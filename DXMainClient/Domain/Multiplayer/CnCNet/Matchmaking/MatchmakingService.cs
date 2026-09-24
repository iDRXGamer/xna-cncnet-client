#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

using ClientCore.Extensions;

using ClientGUI;

using DTAClient.Online;

using Rampastring.Tools;
using Rampastring.XNAUI;

namespace DTAClient.Domain.Multiplayer.CnCNet.Matchmaking
{
    /// <summary>
    /// Runs a casual matchmaking search: joins the queue, polls the server until a match
    /// is found and then launches the game.
    /// All state is changed on the UI thread; network requests run in the background and
    /// their results are passed back through <see cref="WindowManager.AddCallback"/>.
    /// </summary>
    public sealed class MatchmakingService : IDisposable
    {
        private const string PROTOCOL_VERSION = "2.0";
        private const int GAME_PORT = 50000;
        private const int MIN_CHECKBACK_SECONDS = 5;
        private const int MAX_RETRIES = 3;
        private const int FIRST_RETRY_DELAY_SECONDS = 2;

        private readonly MatchmakingApiService apiService;
        private readonly ApiSpawnService spawnService;
        private readonly WindowManager windowManager;
        private readonly Func<string> localPlayerNameProvider;
        private readonly Func<bool> canJoinQueueProvider;
        private readonly int side;

        private readonly Timer pollTimer;

        private bool isHashingFiles;
        private int searchId;
        private int consecutiveErrors;
        private MatchmakingModeInfo? searchMode;
        private string? searchToken;
        private string? sessionFileHash;

        public MatchmakingService(
            MatchmakingApiService apiService,
            ApiSpawnService spawnService,
            WindowManager windowManager,
            Func<string> localPlayerNameProvider,
            Func<bool> canJoinQueueProvider,
            int side)
        {
            this.apiService = apiService;
            this.spawnService = spawnService;
            this.windowManager = windowManager;
            this.localPlayerNameProvider = localPlayerNameProvider;
            this.canJoinQueueProvider = canJoinQueueProvider;
            this.side = side;

            SelectedMode = MatchmakingConfig.Instance.Modes.Count > 0 ? MatchmakingConfig.Instance.Modes[0] : null;

            pollTimer = new Timer { AutoReset = false };
            pollTimer.Elapsed += PollTimer_Elapsed;
        }

        /// <summary>
        /// Raised when a search starts or stops.
        /// </summary>
        public event EventHandler? SearchStateChanged;

        /// <summary>
        /// Raised with a message that should be shown to the user.
        /// </summary>
        public event EventHandler<string>? Notice;

        public bool IsInQueue { get; private set; }

        /// <summary>
        /// True while a search is being prepared or is running. The selected mode cannot be changed then.
        /// </summary>
        public bool IsSearching => IsInQueue || isHashingFiles;

        public DateTime? QueueStartTime { get; private set; }

        public MatchmakingModeInfo? SelectedMode { get; private set; }

        private string LocalPlayerName => localPlayerNameProvider();

        public void SelectMode(MatchmakingModeInfo mode)
        {
            if (!IsSearching)
                SelectedMode = mode;
        }

        public Task<Dictionary<string, int>?> GetQueueCountsAsync() => apiService.GetQueueCountsAsync();

        public Task<List<string>?> GetMapNamesAsync(string ladder) => apiService.GetMapNamesAsync(ladder);

        public async void StartQueue()
        {
            if (IsSearching || SelectedMode == null)
                return;

            if (!canJoinQueueProvider())
            {
                RaiseNotice("Cannot join matchmaking queue while in a game room or loading.".L10N("Client:Matchmaking:CannotJoinInRoom"));
                return;
            }

            // Set before the first await so that another click is ignored while hashing
            isHashingFiles = true;
            SearchStateChanged?.Invoke(this, EventArgs.Empty);

            string? fileHash = await CalculateFileHashAsync();

            // Continue on the UI thread
            windowManager.AddCallback(new Action(() => OnFileHashCalculated(fileHash)));
        }

        private static async Task<string?> CalculateFileHashAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    var fileHashCalculator = new FileHashCalculator();
                    fileHashCalculator.CalculateHashes();

                    return fileHashCalculator.GetCompleteHash();
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"[Matchmaking] Failed to calculate file hashes: {ex.Message}");
                return null;
            }
        }

        private void OnFileHashCalculated(string? fileHash)
        {
            isHashingFiles = false;

            if (fileHash == null)
            {
                RaiseNotice("Failed to verify game files. Matchmaking search was not started.".L10N("Client:Matchmaking:FileHashError"));
                SearchStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Same anti-cheat check as the CnCNet lobby: game files must not change during a session
            if (sessionFileHash != null && sessionFileHash != fileHash)
            {
                Logger.Log("[Matchmaking] Modified game files detected during client session!");
                RaiseNotice(string.Format("{0} has modified game files during the client session. They are likely attempting to cheat!".L10N("Client:Main:PlayerModifyFileCheat"), LocalPlayerName));
                SearchStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            sessionFileHash = fileHash;
            searchMode = SelectedMode;

            if (searchMode == null)
            {
                SearchStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            searchId++;
            searchToken = Guid.NewGuid().ToString("N");
            consecutiveErrors = 0;
            IsInQueue = true;
            QueueStartTime = DateTime.UtcNow;

            Logger.Log($"[Matchmaking] Searching for a {searchMode.Id} match on ladder {searchMode.Ladder}.");
            RaiseNotice("Searching for opponent...".L10N("Client:Matchmaking:SearchingNotice"));
            SearchStateChanged?.Invoke(this, EventArgs.Empty);

            SendMatchRequest(searchId);
        }

        public void LeaveQueue(bool notifyServer)
        {
            if (!IsInQueue || searchMode == null)
                return;

            string ladder = searchMode.Ladder;

            StopSearch();
            RaiseNotice("Matchmaking search cancelled.".L10N("Client:Matchmaking:CancelledNotice"));

            if (notifyServer)
                _ = SendQuitRequestAsync(ladder, LocalPlayerName);
        }

        private async Task SendQuitRequestAsync(string ladder, string playerName)
        {
            QmMatchRequest request = CreateMatchRequest("quit");
            QmMatchResponse response = await apiService.SendMatchRequestAsync(ladder, playerName, request).ConfigureAwait(false);

            Logger.Log($"[Matchmaking] Quit request answered with '{response.Type}'.");
        }

        private void StopSearch()
        {
            pollTimer.Stop();
            searchId++;
            IsInQueue = false;
            QueueStartTime = null;
            consecutiveErrors = 0;
            SearchStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PollTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            int currentSearchId = searchId;
            windowManager.AddCallback(new Action(() => SendMatchRequest(currentSearchId)));
        }

        private async void SendMatchRequest(int requestSearchId)
        {
            if (!IsInQueue || requestSearchId != searchId || searchMode == null)
                return;

            QmMatchResponse response = await apiService.SendMatchRequestAsync(searchMode.Ladder, LocalPlayerName, CreateMatchRequest("match me up"));

            windowManager.AddCallback(new Action(() => HandleResponse(requestSearchId, response)));
        }

        private void HandleResponse(int responseSearchId, QmMatchResponse response)
        {
            // The search was cancelled or restarted while the request was in flight
            if (!IsInQueue || responseSearchId != searchId)
                return;

            switch (response.Type)
            {
                case "please wait":
                case "checkback":
                    consecutiveErrors = 0;
                    ScheduleNextRequest(Math.Max(response.CheckBack, MIN_CHECKBACK_SECONDS));
                    break;

                case "spawn":
                    OnMatchFound(response);
                    break;

                case "quit":
                    StopSearch();
                    break;

                case "fatal":
                    StopSearch();
                    RaiseError(response);
                    break;

                default:
                    if (consecutiveErrors < MAX_RETRIES)
                    {
                        int delaySeconds = FIRST_RETRY_DELAY_SECONDS << consecutiveErrors;
                        consecutiveErrors++;
                        Logger.Log($"[Matchmaking] Request failed ({response.Description ?? response.Type}), retry {consecutiveErrors}/{MAX_RETRIES} in {delaySeconds}s.");
                        ScheduleNextRequest(delaySeconds);
                        break;
                    }

                    StopSearch();
                    RaiseError(response);
                    break;
            }
        }

        private void ScheduleNextRequest(int delaySeconds)
        {
            pollTimer.Interval = delaySeconds * 1000;
            pollTimer.Start();
        }

        private void OnMatchFound(QmMatchResponse response)
        {
            Logger.Log("[Matchmaking] Match found.");
            StopSearch();

            if (!spawnService.WriteSpawnFiles(response))
            {
                RaiseNotice("Error preparing match files.".L10N("Client:Matchmaking:SpawnError"));
                return;
            }

            RaiseNotice("Match found! Launching game...".L10N("Client:Matchmaking:MatchFoundNotice"));
            GameProcessLogic.StartGameProcess(windowManager);
        }

        private void RaiseError(QmMatchResponse response)
        {
            string description = response.Description ?? response.Message ?? response.Type ?? string.Empty;
            RaiseNotice(string.Format("Matchmaking error: {0}".L10N("Client:Matchmaking:ErrorFormat"), description));
        }

        private void RaiseNotice(string message) => Notice?.Invoke(this, message);

        private QmMatchRequest CreateMatchRequest(string type)
        {
            return new QmMatchRequest
            {
                Type = type,
                Side = side,
                Version = PROTOCOL_VERSION,
                ClientVersion = sessionFileHash != null && sessionFileHash.Length > 32 ? sessionFileHash.Substring(0, 32) : sessionFileHash,
                IpPort = GAME_PORT,
                SearchToken = searchToken
            };
        }

        public void Dispose()
        {
            pollTimer.Dispose();
            apiService.Dispose();
        }
    }
}

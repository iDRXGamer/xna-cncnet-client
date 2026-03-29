#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    internal sealed class MatchmakingService
    {
        public const string ChannelCommandName = "MMQ";
        public const string PrivateJoinCommandName = "MMJOIN";

        private const string CommandJoin = "JOIN";
        private const string CommandLeave = "LEAVE";
        private const string CommandMatch = "MATCH";

        private readonly Random random;
        private readonly MatchmakingLogger logger;

        private readonly Func<string>? selectedModeProvider;
        private readonly Func<bool> canJoinQueue;
        private readonly Func<bool> canHostMatch;
        private readonly Func<string, int> requiredPlayersForMode;
        private readonly Action<string> sendQueueCommand;
        private readonly Action<string> addNotice;
        private readonly Action<bool> setQueueUiState;
        private readonly Action<string, List<string>> localMatchClaimedCallback;
        private readonly Func<string>? localPlayerNameProvider;

        private readonly Dictionary<string, List<QueueEntry>> queues =
            new Dictionary<string, List<QueueEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> handledMatchIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pendingClaimIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool isInQueue;
        private string? queueMode;
        private string? queueTicket;
        private DateTime lastActionTime = DateTime.MinValue;
        private bool isBusy;
        private const double ActionCooldownMs = 2000;

        public MatchmakingService(
            Random random,
            Func<string>? localPlayerNameProvider,
            MatchmakingLogger logger,
            Func<string>? selectedModeProvider,
            Func<bool> canJoinQueue,
            Func<bool> canHostMatch,
            Func<string, int> requiredPlayersForMode,
            Action<string> sendQueueCommand,
            Action<string> addNotice,
            Action<bool> setQueueUiState,
            Action<string, List<string>> localMatchClaimedCallback)
        {
            this.random = random;
            this.localPlayerNameProvider = localPlayerNameProvider;
            this.logger = logger;
            this.selectedModeProvider = selectedModeProvider;
            this.canJoinQueue = canJoinQueue;
            this.canHostMatch = canHostMatch;
            this.requiredPlayersForMode = requiredPlayersForMode;
            this.sendQueueCommand = sendQueueCommand;
            this.addNotice = addNotice;
            this.setQueueUiState = setQueueUiState;
            this.localMatchClaimedCallback = localMatchClaimedCallback;
        }

        public bool IsInQueue => isInQueue;

        private string LocalPlayerName => localPlayerNameProvider?.Invoke() ?? string.Empty;

        public void ToggleQueue()
        {
            if (DateTime.Now.Subtract(lastActionTime).TotalMilliseconds < ActionCooldownMs)
            {
                logger.Warn("ActionThrottled", $"cooldown_remaining={ActionCooldownMs - DateTime.Now.Subtract(lastActionTime).TotalMilliseconds}ms");
                return;
            }

            if (isBusy)
            {
                logger.Warn("ActionBlocked", "is_busy");
                return;
            }

            if (isInQueue)
            {
                LeaveQueue(true, true);
                return;
            }

            StartQueue();
        }

        public void StartQueue()
        {
            if (isInQueue)
                return;

            if (DateTime.Now.Subtract(lastActionTime).TotalMilliseconds < ActionCooldownMs)
                return;

            if (!canJoinQueue())
            {
                addNotice("Cannot join matchmaking queue while already joining or inside a game room.");
                logger.Warn("QueueJoinRejected", "client_not_ready");
                return;
            }

            string mode = selectedModeProvider?.Invoke() ?? string.Empty;

            if (string.IsNullOrEmpty(mode))
            {
                logger.Warn("QueueJoinRejected", "empty_mode");
                return;
            }

            string localPlayerName = LocalPlayerName;

            if (string.IsNullOrEmpty(localPlayerName))
            {
                logger.Warn("QueueJoinRejected", "empty_local_player_name");
                addNotice("Cannot join matchmaking queue: missing local player name.");
                return;
            }

            isBusy = true;
            lastActionTime = DateTime.Now;

            isInQueue = true;
            queueMode = mode;
            queueTicket = $"{DateTime.UtcNow.Ticks}-{random.Next(1000, 9999)}";
            setQueueUiState(true);

            AddOrUpdateQueueEntry(localPlayerName, mode, queueTicket);
            sendQueueCommand($"{CommandJoin};{mode};{queueTicket}");
            addNotice($"Joined matchmaking queue ({mode}).");

            logger.Info("QueueJoined", $"mode={mode}, ticket={queueTicket}");
            logger.Info("QueueSnapshot", $"mode={mode}, players={GetQueueSnapshot(mode)}");

            isBusy = false;
            TryClaimMatch(mode);
        }

        public void LeaveQueue(bool broadcastLeave, bool showMessage)
        {
            if (!isInQueue)
                return;

            string? mode = queueMode;

            logger.Info("QueueLeaveRequested", $"mode={mode}, broadcastLeave={broadcastLeave}");

            isBusy = true;
            if (broadcastLeave)
                lastActionTime = DateTime.Now;

            ClearQueueState(updateUiState: true);

            if (!string.IsNullOrEmpty(mode))
            {
                string localPlayerName = LocalPlayerName;

                if (!string.IsNullOrEmpty(localPlayerName))
                    RemoveQueueEntryFromMode(localPlayerName, mode);

                if (broadcastLeave)
                    sendQueueCommand($"{CommandLeave};{mode}");
            }

            if (showMessage)
                addNotice("Left matchmaking queue.");

            isBusy = false;
        }

        public void Reset()
        {
            logger.Info("ResetState");
            queues.Clear();
            handledMatchIds.Clear();
            pendingClaimIds.Clear();
            ClearQueueState(updateUiState: true);
        }

        public void HandleChannelCommand(string sender, string commandData)
        {
            if (string.IsNullOrEmpty(commandData))
                return;

            string[] parts = commandData.Split(';');

            if (parts.Length == 0)
                return;

            logger.Info("QueueCommandReceived", $"sender={sender}, payload={commandData}");

            switch (parts[0])
            {
                case CommandJoin:
                    if (parts.Length >= 3)
                        HandleQueueJoin(sender, parts[1], parts[2]);
                    return;
                case CommandLeave:
                    if (parts.Length >= 2)
                        HandleQueueLeave(sender, parts[1]);
                    return;
                case CommandMatch:
                    if (parts.Length >= 4)
                        HandleMatchClaim(sender, parts[1], parts[2], parts[3]);
                    return;
                default:
                    logger.Warn("QueueCommandIgnored", $"sender={sender}, payload={commandData}");
                    return;
            }
        }

        public void HandleUserLeftOrQuit(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return;

            logger.Info("UserLeftQueueChannels", $"player={playerName}");

            RemovePlayerFromAllQueues(playerName);

            foreach (string mode in queues.Keys.ToList())
            {
                TryClaimMatch(mode);
            }
        }

        private void HandleQueueJoin(string sender, string mode, string ticket)
        {
            if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(ticket))
                return;

            AddOrUpdateQueueEntry(sender, mode, ticket);

            logger.Info("QueueJoinApplied", $"sender={sender}, mode={mode}, ticket={ticket}");
            logger.Info("QueueSnapshot", $"mode={mode}, players={GetQueueSnapshot(mode)}");

            TryClaimMatch(mode);
        }

        private void HandleQueueLeave(string sender, string mode)
        {
            if (string.IsNullOrEmpty(mode))
                return;

            RemoveQueueEntryFromMode(sender, mode);

            logger.Info("QueueLeaveApplied", $"sender={sender}, mode={mode}");
            logger.Info("QueueSnapshot", $"mode={mode}, players={GetQueueSnapshot(mode)}");

            if (string.Equals(sender, LocalPlayerName, StringComparison.OrdinalIgnoreCase))
                ClearQueueState(updateUiState: true);

            TryClaimMatch(mode);
        }

        private void HandleMatchClaim(string sender, string mode, string matchId, string playerList)
        {
            if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(playerList))
                return;

            pendingClaimIds.Remove(matchId);

            if (!handledMatchIds.Add(matchId))
            {
                logger.Info("MatchClaimIgnored", $"reason=already_handled, matchId={matchId}");
                return;
            }

            List<string> participants = playerList
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (participants.Count != requiredPlayersForMode(mode))
            {
                logger.Warn("MatchClaimIgnored", $"reason=invalid_count, matchId={matchId}, mode={mode}, participants={participants.Count}");
                return;
            }

            participants = participants
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!participants.Contains(sender, StringComparer.OrdinalIgnoreCase))
            {
                logger.Warn("MatchClaimIgnored", $"reason=sender_not_in_match, sender={sender}, matchId={matchId}");
                return;
            }

            string designatedHost = participants[0];

            logger.Info("MatchClaimAccepted", $"matchId={matchId}, mode={mode}, sender={sender}, host={designatedHost}, participants={string.Join(",", participants)}");

            RemovePlayersFromQueues(participants);

            bool localPlayerInMatch = participants.Any(p =>
                string.Equals(p, LocalPlayerName, StringComparison.OrdinalIgnoreCase));

            if (localPlayerInMatch)
                ClearQueueState(updateUiState: true);

            if (string.Equals(LocalPlayerName, designatedHost, StringComparison.OrdinalIgnoreCase))
            {
                localMatchClaimedCallback(mode, participants);
            }
            else if (localPlayerInMatch)
            {
                logger.Info("MatchClaimAwaitingHost", $"matchId={matchId}, mode={mode}, expectedHost={designatedHost}");
            }

            TryClaimMatch(mode);
        }

        private void TryClaimMatch(string mode)
        {
            if (string.IsNullOrEmpty(mode))
                return;

            if (!queues.TryGetValue(mode, out List<QueueEntry>? queue))
            {
                logger.Info("MatchClaimSkipped", $"mode={mode}, reason=queue_missing");
                return;
            }

            int requiredPlayers = requiredPlayersForMode(mode);

            if (queue == null || queue.Count < requiredPlayers)
            {
                logger.Info("MatchClaimWaiting", $"mode={mode}, queued={queue?.Count ?? 0}, required={requiredPlayers}");
                return;
            }

            List<QueueEntry> participants = queue
                .OrderBy(qe => qe.PlayerName, StringComparer.OrdinalIgnoreCase)
                .Take(requiredPlayers)
                .ToList();

            string localPlayerName = LocalPlayerName;

            if (!participants.Any(p => string.Equals(p.PlayerName, localPlayerName, StringComparison.OrdinalIgnoreCase)))
            {
                logger.Info("MatchClaimSkipped", $"mode={mode}, reason=local_not_in_participants, local={localPlayerName}, participants={string.Join(",", participants.Select(p => p.PlayerName))}");
                return;
            }

            if (!canHostMatch())
            {
                logger.Info("MatchClaimSkipped", $"mode={mode}, reason=cannot_host_now, local={localPlayerName}");
                return;
            }

            string matchId = $"{mode}:{string.Join("|", participants.Select(p => p.PlayerName + ":" + p.Ticket))}";

            if (handledMatchIds.Contains(matchId) || pendingClaimIds.Contains(matchId))
            {
                logger.Info("MatchClaimSkipped", $"mode={mode}, reason=already_pending_or_handled, matchId={matchId}");
                return;
            }

            pendingClaimIds.Add(matchId);

            string participantList = string.Join(",", participants.Select(p => p.PlayerName));

            logger.Info("MatchClaimBroadcast", $"matchId={matchId}, mode={mode}, sender={localPlayerName}, participants={participantList}");

            sendQueueCommand($"{CommandMatch};{mode};{matchId};{participantList}");

            // Process local claim immediately so host creation doesn't depend on IRC echo behavior.
            HandleMatchClaim(localPlayerName, mode, matchId, participantList);
        }

        private string GetQueueSnapshot(string mode)
        {
            if (string.IsNullOrEmpty(mode) || !queues.TryGetValue(mode, out List<QueueEntry>? queue) || queue == null || queue.Count == 0)
                return "(empty)";

            return string.Join(",", queue
                .OrderBy(q => q.PlayerName, StringComparer.OrdinalIgnoreCase)
                .Select(q => $"{q.PlayerName}:{q.Ticket}"));
        }

        private void AddOrUpdateQueueEntry(string playerName, string mode, string ticket)
        {
            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(ticket))
                return;

            RemovePlayerFromAllQueues(playerName);

            if (!queues.TryGetValue(mode, out List<QueueEntry>? queue) || queue == null)
            {
                queue = new List<QueueEntry>();
                queues[mode] = queue;
            }

            queue.Add(new QueueEntry
            {
                PlayerName = playerName,
                Ticket = ticket
            });
        }

        private void RemoveQueueEntryFromMode(string playerName, string mode)
        {
            if (!queues.TryGetValue(mode, out List<QueueEntry>? queue) || queue == null)
                return;

            queue.RemoveAll(qe => string.Equals(qe.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));

            if (queue.Count == 0)
                queues.Remove(mode);
        }

        private void RemovePlayerFromAllQueues(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return;

            foreach (string mode in queues.Keys.ToList())
            {
                RemoveQueueEntryFromMode(playerName, mode);
            }
        }

        private void RemovePlayersFromQueues(List<string> playerNames)
        {
            foreach (string playerName in playerNames)
            {
                RemovePlayerFromAllQueues(playerName);
            }
        }

        private void ClearQueueState(bool updateUiState)
        {
            isInQueue = false;
            queueMode = null;
            queueTicket = null;

            if (updateUiState)
                setQueueUiState(false);
        }

        private sealed class QueueEntry
        {
            public string PlayerName { get; set; } = string.Empty;
            public string Ticket { get; set; } = string.Empty;
        }
    }
}

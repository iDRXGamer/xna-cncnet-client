#nullable enable

using System;
using System.Collections.Generic;

using ClientCore.Extensions;

using ClientGUI;

using DTAClient.Domain.Multiplayer.CnCNet.Matchmaking;

using Microsoft.Xna.Framework;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    /// <summary>
    /// Lets the player pick a casual matchmaking mode and start or cancel a search.
    /// </summary>
    public class MatchmakingWindow : XNAWindow
    {
        private static readonly TimeSpan QueueCountsRefreshInterval = TimeSpan.FromSeconds(15);

        private readonly MatchmakingService matchmakingService;

        private XNAListBox lbModes = null!;
        private XNALabel lblModeDescription = null!;
        private XNALabel lblMaps = null!;
        private XNALabel lblStatus = null!;
        private XNAClientButton btnFindMatch = null!;

        private TimeSpan timeSinceQueueCountsRefresh = TimeSpan.Zero;
        private bool isFetchingQueueCounts;
        private Dictionary<string, int> queueCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> mapNamesByLadder = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private int displayedSearchSeconds = -1;

        public MatchmakingWindow(WindowManager windowManager, MatchmakingService matchmakingService) : base(windowManager)
        {
            this.matchmakingService = matchmakingService;
        }

        public override void Initialize()
        {
            Name = nameof(MatchmakingWindow);
            ClientRectangle = new Rectangle(0, 0, 560, 360);
            BackgroundTexture = AssetLoader.LoadTexture("gamecreationoptionsbg.png");
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            var lblTitle = new XNALabel(WindowManager);
            lblTitle.Name = nameof(lblTitle);
            lblTitle.ClientRectangle = new Rectangle(16, 14, 0, 0);
            lblTitle.FontIndex = 1;
            lblTitle.Text = "CASUAL MATCHMAKING".L10N("Client:Matchmaking:Title");
            AddChild(lblTitle);

            var lblSelectMode = new XNALabel(WindowManager);
            lblSelectMode.Name = nameof(lblSelectMode);
            lblSelectMode.ClientRectangle = new Rectangle(16, 42, 0, 0);
            lblSelectMode.Text = "Select Game Mode:".L10N("Client:Matchmaking:SelectMode");
            AddChild(lblSelectMode);

            lbModes = new XNAListBox(WindowManager);
            lbModes.Name = nameof(lbModes);
            lbModes.ClientRectangle = new Rectangle(16, 62, 528, 110);
            lbModes.SelectedIndexChanged += LbModes_SelectedIndexChanged;
            AddChild(lbModes);

            lblModeDescription = new XNALabel(WindowManager);
            lblModeDescription.Name = nameof(lblModeDescription);
            lblModeDescription.ClientRectangle = new Rectangle(16, 184, 528, 60);
            AddChild(lblModeDescription);

            lblMaps = new XNALabel(WindowManager);
            lblMaps.Name = nameof(lblMaps);
            lblMaps.ClientRectangle = new Rectangle(16, 252, 528, 30);
            AddChild(lblMaps);

            lblStatus = new XNALabel(WindowManager);
            lblStatus.Name = nameof(lblStatus);
            lblStatus.ClientRectangle = new Rectangle(16, 292, 0, 0);
            AddChild(lblStatus);

            btnFindMatch = new XNAClientButton(WindowManager);
            btnFindMatch.Name = nameof(btnFindMatch);
            btnFindMatch.ClientRectangle = new Rectangle(16, Height - 36, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnFindMatch.LeftClick += BtnFindMatch_LeftClick;
            AddChild(btnFindMatch);

            var btnClose = new XNAClientButton(WindowManager);
            btnClose.Name = nameof(btnClose);
            btnClose.ClientRectangle = new Rectangle(Width - 16 - UIDesignConstants.BUTTON_WIDTH_92, btnFindMatch.Y, UIDesignConstants.BUTTON_WIDTH_92, UIDesignConstants.BUTTON_HEIGHT);
            btnClose.Text = "Close".L10N("Client:Main:ButtonClose");
            btnClose.LeftClick += BtnClose_LeftClick;
            AddChild(btnClose);

            base.Initialize();

            CenterOnParent();

            foreach (MatchmakingModeInfo mode in MatchmakingConfig.Instance.Modes)
                lbModes.AddItem(new XNAListBoxItem(mode.UIName) { Tag = mode });

            lbModes.SelectedIndex = MatchmakingConfig.Instance.Modes.IndexOf(matchmakingService.SelectedMode!);

            matchmakingService.SearchStateChanged += (s, e) => RefreshSearchState();
        }

        public void Open()
        {
            Enable();
            RefreshSearchState();
            RefreshQueueCounts();
        }

        private void RefreshSearchState()
        {
            bool isSearching = matchmakingService.IsSearching;

            btnFindMatch.Text = isSearching
                ? "Cancel Search".L10N("Client:Matchmaking:CancelSearch")
                : "Find Match".L10N("Client:Matchmaking:FindMatch");
            btnFindMatch.AllowClick = !isSearching || matchmakingService.IsInQueue;
            lbModes.AllowKeyboardInput = !isSearching;
            lbModes.Enabled = !isSearching;

            displayedSearchSeconds = -1;
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (!matchmakingService.IsInQueue || !matchmakingService.QueueStartTime.HasValue)
            {
                lblStatus.Text = matchmakingService.IsSearching
                    ? "Status: Verifying game files...".L10N("Client:Matchmaking:StatusVerifying")
                    : "Status: Ready".L10N("Client:Matchmaking:StatusReady");
                return;
            }

            TimeSpan elapsed = DateTime.UtcNow - matchmakingService.QueueStartTime.Value;
            int elapsedSeconds = (int)elapsed.TotalSeconds;

            // Only rebuild the text when the displayed time changes
            if (elapsedSeconds == displayedSearchSeconds)
                return;

            displayedSearchSeconds = elapsedSeconds;
            lblStatus.Text = string.Format("Status: Searching for {0}... ({1:D2}:{2:D2})".L10N("Client:Matchmaking:StatusSearching"),
                matchmakingService.SelectedMode?.UIName, elapsed.Minutes, elapsed.Seconds);
        }

        private void LbModes_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (lbModes.SelectedItem?.Tag is not MatchmakingModeInfo mode)
                return;

            matchmakingService.SelectMode(mode);
            lblModeDescription.Text = mode.Description;
            ShowMapNames(mode);
        }

        /// <summary>
        /// Shows the map pool of the mode's ladder, which is set on the ladder API.
        /// </summary>
        private async void ShowMapNames(MatchmakingModeInfo mode)
        {
            if (mapNamesByLadder.TryGetValue(mode.Ladder, out List<string>? knownMapNames))
            {
                lblMaps.Text = FormatMapNames(knownMapNames);
                return;
            }

            lblMaps.Text = string.Empty;
            List<string>? mapNames = await matchmakingService.GetMapNamesAsync(mode.Ladder);

            WindowManager.AddCallback(new Action(() =>
            {
                if (mapNames == null)
                    return;

                mapNamesByLadder[mode.Ladder] = mapNames;

                // Another mode may have been selected in the meantime
                if (matchmakingService.SelectedMode == mode)
                    lblMaps.Text = FormatMapNames(mapNames);
            }));
        }

        private static string FormatMapNames(List<string> mapNames)
            => mapNames.Count > 0 ? "Maps:".L10N("Client:Matchmaking:Maps") + " " + string.Join(", ", mapNames) : string.Empty;

        private async void RefreshQueueCounts()
        {
            if (isFetchingQueueCounts)
                return;

            isFetchingQueueCounts = true;
            Dictionary<string, int>? counts = await matchmakingService.GetQueueCountsAsync();

            WindowManager.AddCallback(new Action(() =>
            {
                isFetchingQueueCounts = false;

                if (counts == null)
                    return;

                queueCounts = new Dictionary<string, int>(counts, StringComparer.OrdinalIgnoreCase);
                UpdateModeItemTexts();
            }));
        }

        private void UpdateModeItemTexts()
        {
            foreach (XNAListBoxItem item in lbModes.Items)
            {
                if (item.Tag is not MatchmakingModeInfo mode)
                    continue;

                int count = queueCounts.TryGetValue(mode.Ladder, out int c) ? c : 0;
                item.Text = mode.UIName + "  " + string.Format("[{0} in queue]".L10N("Client:Matchmaking:InQueue"), count);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Enabled || !Visible)
                return;

            UpdateStatusText();

            timeSinceQueueCountsRefresh += gameTime.ElapsedGameTime;
            if (timeSinceQueueCountsRefresh >= QueueCountsRefreshInterval)
            {
                timeSinceQueueCountsRefresh = TimeSpan.Zero;
                RefreshQueueCounts();
            }
        }

        private void BtnFindMatch_LeftClick(object? sender, EventArgs e)
        {
            if (matchmakingService.IsInQueue)
                matchmakingService.LeaveQueue(true);
            else
                matchmakingService.StartQueue();
        }

        private void BtnClose_LeftClick(object? sender, EventArgs e)
        {
            // Closing the window cancels the search so that a game does not start unexpectedly later
            matchmakingService.LeaveQueue(true);
            Disable();
        }
    }
}

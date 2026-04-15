using Rampastring.XNAUI.XNAControls;
using System;
using ClientCore;
using Rampastring.XNAUI;
using ClientGUI;
using ClientCore.Extensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Color = Microsoft.Xna.Framework.Color;

namespace DTAClient.DXGUI.Generic
{
    /// <summary>
    /// Displays a dialog in the client when a matchmaking match is found.
    /// Blocks user interaction until the game process starts.
    /// </summary>
    public class MatchFoundWindow : XNAPanel
    {
        public MatchFoundWindow(WindowManager windowManager) : base(windowManager)
        {
        }

        private bool initialized = false;

        public override void Initialize()
        {
            if (initialized)
                throw new InvalidOperationException("MatchFoundWindow cannot be initialized twice!");

            initialized = true;

            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            DrawBorders = false;
            ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX, WindowManager.RenderResolutionY);

            XNAWindow window = new XNAWindow(WindowManager);
            window.Name = "MatchFoundWindowBox";
            window.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 220), 2, 2);
            window.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            window.ClientRectangle = new Rectangle(0, 0, 350, 120);

            XNALabel explanation = new XNALabel(WindowManager);
            explanation.FontIndex = 1;
            explanation.Text = "Match found! Joining room...".L10N("Client:Main:MatchFoundLabel");

            AddChild(window);
            window.AddChild(explanation);

            base.Initialize();

            explanation.CenterOnParent();
            window.CenterOnParent();

            Visible = false;
            Enabled = false;

            GameProcessLogic.GameProcessStarted += SharedUILogic_GameProcessStarted;
        }

        private void SharedUILogic_GameProcessStarted()
        {
            Hide();
        }

        public void Show()
        {
            Visible = true;
            Enabled = true;
        }

        public void Hide()
        {
            Visible = false;
            Enabled = false;
        }
    }
}

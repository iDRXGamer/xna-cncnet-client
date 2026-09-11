#nullable enable

using System;
using System.IO;
using ClientCore;
using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.Matchmaking
{
    public class MatchmakingSettings
    {
        private static MatchmakingSettings? instance;

        public static MatchmakingSettings Instance => instance ??= new MatchmakingSettings();

        public bool Enabled { get; private set; }

        public string ApiUrl { get; private set; } = "http://localhost:3000";

        public string Ladder { get; private set; } = "sim-ra2";

        public bool Casual { get; private set; } = true;

        public int DefaultSide { get; private set; } = 0;

        private MatchmakingSettings()
        {
            Initialize();
        }

        public void Initialize()
        {
            string iniPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "INI", "Matchmaking.ini");
            FileInfo fileInfo = SafePath.GetFile(iniPath);

            if (!fileInfo.Exists)
            {
                Enabled = false;
                Logger.Log($"[Matchmaking] Config not found at {iniPath}. Matchmaking disabled.");

                return;
            }

            IniFile ini = new IniFile(iniPath);

            Enabled = ini.GetBooleanValue("Matchmaking", "Enabled", true);
            ApiUrl = ini.GetStringValue("Matchmaking", "ApiUrl", "http://localhost:3000");
            Ladder = ini.GetStringValue("Matchmaking", "Ladder", "sim-ra2");
            Casual = ini.GetBooleanValue("Matchmaking", "Casual", true);
            DefaultSide = ini.GetIntValue("Matchmaking", "DefaultSide", 0);

            Logger.Log($"[Matchmaking] Initialized: Enabled={Enabled}, ApiUrl={ApiUrl}, Ladder={Ladder}, Casual={Casual}");
        }
    }
}

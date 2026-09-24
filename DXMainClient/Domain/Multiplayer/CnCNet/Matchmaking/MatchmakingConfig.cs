#nullable enable

using System;
using System.Collections.Generic;

using ClientCore;

using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.Matchmaking
{
    /// <summary>
    /// A casual matchmaking mode, defined by a section in INI/Matchmaking.ini.
    /// </summary>
    public class MatchmakingModeInfo
    {
        public string Id { get; set; } = string.Empty;

        public string UIName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Abbreviation of the ladder API ladder that players of this mode queue on.
        /// The map pool and the game rules of the mode are set on that ladder.
        /// </summary>
        public string Ladder { get; set; } = string.Empty;
    }

    /// <summary>
    /// Casual matchmaking configuration, read from INI/Matchmaking.ini.
    /// Matchmaking is disabled unless the file defines an API URL and at least one mode.
    /// </summary>
    public class MatchmakingConfig
    {
        private const string MATCHMAKING_SECTION = "Matchmaking";
        private const string MODES_SECTION = "MatchmakingModes";

        private static MatchmakingConfig? instance;

        public static MatchmakingConfig Instance => instance ??= new MatchmakingConfig();

        public bool Enabled { get; private set; }

        public string ApiUrl { get; private set; } = string.Empty;

        public int DefaultSide { get; private set; }

        public List<MatchmakingModeInfo> Modes { get; } = new List<MatchmakingModeInfo>();

        private MatchmakingConfig()
        {
            string iniPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "INI", "Matchmaking.ini");

            if (!SafePath.GetFile(iniPath).Exists)
            {
                Logger.Log("[Matchmaking] INI/Matchmaking.ini not found, matchmaking is disabled.");
                return;
            }

            var ini = new IniFile(iniPath);

            ApiUrl = ini.GetStringValue(MATCHMAKING_SECTION, "ApiUrl", string.Empty);
            DefaultSide = ini.GetIntValue(MATCHMAKING_SECTION, "DefaultSide", 0);

            IniSection? modesSection = ini.GetSection(MODES_SECTION);
            if (modesSection != null)
            {
                foreach (KeyValuePair<string, string> kvp in modesSection.Keys)
                {
                    MatchmakingModeInfo? mode = ReadMode(ini, kvp.Value.Trim());
                    if (mode != null)
                        Modes.Add(mode);
                }
            }

            Enabled = ini.GetBooleanValue(MATCHMAKING_SECTION, "Enabled", true) &&
                !string.IsNullOrWhiteSpace(ApiUrl) &&
                Modes.Count > 0;

            Logger.Log($"[Matchmaking] Loaded {Modes.Count} matchmaking modes. Enabled: {Enabled}");
        }

        private static MatchmakingModeInfo? ReadMode(IniFile ini, string modeId)
        {
            IniSection? section = string.IsNullOrEmpty(modeId) ? null : ini.GetSection(modeId);
            if (section == null)
            {
                Logger.Log($"[Matchmaking] Mode section [{modeId}] not found, the mode is ignored.");
                return null;
            }

            string ladder = section.GetStringValue("Ladder", string.Empty);
            if (string.IsNullOrWhiteSpace(ladder))
            {
                Logger.Log($"[Matchmaking] Mode [{modeId}] does not define a Ladder, the mode is ignored.");
                return null;
            }

            return new MatchmakingModeInfo
            {
                Id = modeId,
                UIName = section.GetStringValue("UIName", modeId),
                Description = section.GetStringValue("Description", string.Empty).Replace("@", Environment.NewLine),
                Ladder = ladder
            };
        }
    }
}

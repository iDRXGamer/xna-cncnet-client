#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Rampastring.Tools;
using ClientCore;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class MatchmakingModeDefinition
    {
        public string UIName { get; set; } = string.Empty;
        public int PlayerCount { get; set; }
        public string[] AlliedSideNames { get; set; } = Array.Empty<string>();
        public string[] SovietSideNames { get; set; } = Array.Empty<string>();
        public string[] AlliedColors { get; set; } = Array.Empty<string>();
        public string[] SovietColors { get; set; } = Array.Empty<string>();
        public Dictionary<string, bool> ForceCheckboxes { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, string> ForceDropdowns { get; set; } = new Dictionary<string, string>();
        public bool AssignTeams { get; set; }
    }

    public class MatchmakingSettings
    {
        private static MatchmakingSettings? instance;

        public static MatchmakingSettings Instance => instance ??= new MatchmakingSettings();
        
        public bool DebugMode => false;

        public List<MatchmakingModeDefinition> Modes { get; private set; }

        private MatchmakingSettings()
        {
            Modes = new List<MatchmakingModeDefinition>();
        }

        public void Initialize()
        {
            Modes.Clear();
            
            string iniPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "INI", "Matchmaking.ini");
            var fileInfo = SafePath.GetFile(iniPath);
            if (!fileInfo.Exists)
            {
                Logger.Log($"[Matchmaking] Warning: Configuration file not found at {iniPath}. Matchmaking modes will be empty.");
                return;
            }

            IniFile ini = new IniFile(iniPath);
            // DebugMode is now a hardcoded constant above.
            Logger.Log($"[Matchmaking] Settings initialization: DebugMode is {(DebugMode ? "ENABLED" : "DISABLED")} (Hardcoded)");

            List<string> modeKeys = ini.GetSectionKeys("MatchmakingModes");
            
            if (modeKeys == null || modeKeys.Count == 0)
            {
                Logger.Log($"[Matchmaking] Warning: No modes defined in [MatchmakingModes] section of {iniPath}.");
                return;
            }

            foreach (string key in modeKeys)
            {
                string modeSection = ini.GetStringValue("MatchmakingModes", key, string.Empty);
                if (string.IsNullOrEmpty(modeSection) || !ini.SectionExists(modeSection))
                {
                    Logger.Log($"[Matchmaking] Warning: Mode section [{modeSection}] is empty or completely missing. Skipping.");
                    continue;
                }
                
                var mode = new MatchmakingModeDefinition();
                mode.UIName = ini.GetStringValue(modeSection, "UIName", string.Empty);
                mode.PlayerCount = ini.GetIntValue(modeSection, "PlayerCount", 2);
                mode.AssignTeams = ini.GetBooleanValue(modeSection, "AssignTeams", false);
                
                string alliedSides = ini.GetStringValue(modeSection, "AlliedSideNames", string.Empty);
                mode.AlliedSideNames = string.IsNullOrEmpty(alliedSides) ? Array.Empty<string>() : alliedSides.Split(',').Select(s => s.Trim()).ToArray();
                
                string sovietSides = ini.GetStringValue(modeSection, "SovietSideNames", string.Empty);
                mode.SovietSideNames = string.IsNullOrEmpty(sovietSides) ? Array.Empty<string>() : sovietSides.Split(',').Select(s => s.Trim()).ToArray();
                
                string alliedColors = ini.GetStringValue(modeSection, "AlliedColors", string.Empty);
                mode.AlliedColors = string.IsNullOrEmpty(alliedColors) ? Array.Empty<string>() : alliedColors.Split(',').Select(s => s.Trim()).ToArray();
                
                string sovietColors = ini.GetStringValue(modeSection, "SovietColors", string.Empty);
                mode.SovietColors = string.IsNullOrEmpty(sovietColors) ? Array.Empty<string>() : sovietColors.Split(',').Select(s => s.Trim()).ToArray();

                // Read Checkboxes
                string cbSection = modeSection + "_ForceCheckboxes";
                if (ini.SectionExists(cbSection))
                {
                    var cbKeys = ini.GetSectionKeys(cbSection);
                    if (cbKeys != null)
                    {
                        foreach (var cbKey in cbKeys)
                            mode.ForceCheckboxes[cbKey] = ini.GetBooleanValue(cbSection, cbKey, false);
                    }
                }

                // Read Dropdowns
                string ddSection = modeSection + "_ForceDropdowns";
                if (ini.SectionExists(ddSection))
                {
                    var ddKeys = ini.GetSectionKeys(ddSection);
                    if (ddKeys != null)
                    {
                        foreach (var ddKey in ddKeys)
                            mode.ForceDropdowns[ddKey] = ini.GetStringValue(ddSection, ddKey, string.Empty);
                    }
                }
                
                Modes.Add(mode);
                Logger.Log($"[Matchmaking] Loaded mode: {mode.UIName} ({mode.PlayerCount} players).");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Rampastring.Tools;
using ClientCore;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class MatchmakingModeDefinition
    {
        public string UIName { get; set; }
        public int PlayerCount { get; set; }
        public string[] AlliedSideNames { get; set; }
        public string[] SovietSideNames { get; set; }
        public string[] AlliedColors { get; set; }
        public string[] SovietColors { get; set; }
        public Dictionary<string, bool> ForceCheckboxes { get; set; }
        public Dictionary<string, string> ForceDropdowns { get; set; }
        public bool AssignTeams { get; set; }
    }

    public class MatchmakingSettings
    {
        private static MatchmakingSettings instance;
        public static MatchmakingSettings Instance => instance ?? (instance = new MatchmakingSettings());

        public List<MatchmakingModeDefinition> Modes { get; private set; }

        private MatchmakingSettings()
        {
            Modes = new List<MatchmakingModeDefinition>();
        }

        public void Initialize()
        {
            Modes.Clear();
            string iniPath = ProgramConstants.GamePath + "INI/Matchmaking.ini";
            if (!System.IO.File.Exists(iniPath))
            {
                CreateDefaultSettings(iniPath);
            }

            IniFile ini = new IniFile(iniPath);
            List<string> modeSections = ini.GetSectionKeys("MatchmakingModes");
            if (modeSections == null || modeSections.Count == 0)
                return;

            foreach (string modeKey in modeSections)
            {
                string sectionName = ini.GetStringValue("MatchmakingModes", modeKey, string.Empty);
                if (string.IsNullOrEmpty(sectionName)) continue;

                var mode = new MatchmakingModeDefinition();
                mode.UIName = sectionName;
                mode.PlayerCount = ini.GetIntValue(sectionName, "MaxPlayers", 2);
                
                // Enforce even players
                if (mode.PlayerCount % 2 != 0) 
                    mode.PlayerCount++;
                if (mode.PlayerCount > 8) mode.PlayerCount = 8;
                if (mode.PlayerCount < 2) mode.PlayerCount = 2;

                string alliedSides = ini.GetStringValue(sectionName, "AlliedSides", "Allies,Allied");
                mode.AlliedSideNames = alliedSides.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                string sovietSides = ini.GetStringValue(sectionName, "SovietSides", "Soviets,Soviet");
                mode.SovietSideNames = sovietSides.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                string alliedColors = ini.GetStringValue(sectionName, "AlliedColors", "Blue,Cyan,Green,Yellow");
                mode.AlliedColors = alliedColors.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                string sovietColors = ini.GetStringValue(sectionName, "SovietColors", "Red,Orange,Purple,Pink");
                mode.SovietColors = sovietColors.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                mode.AssignTeams = ini.GetBooleanValue(sectionName, "AssignTeams", false);

                mode.ForceCheckboxes = new Dictionary<string, bool>();
                mode.ForceDropdowns = new Dictionary<string, string>();

                List<string> sectionKeys = ini.GetSectionKeys(sectionName);
                if (sectionKeys != null)
                {
                    foreach (string key in sectionKeys)
                    {
                        if (key.StartsWith("chk", StringComparison.OrdinalIgnoreCase))
                        {
                            mode.ForceCheckboxes[key] = ini.GetBooleanValue(sectionName, key, false);
                        }
                        else if (key.StartsWith("cmb", StringComparison.OrdinalIgnoreCase))
                        {
                            mode.ForceDropdowns[key] = ini.GetStringValue(sectionName, key, string.Empty);
                        }
                    }
                }

                Modes.Add(mode);
            }
        }

        private void CreateDefaultSettings(string path)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                var lines = new List<string>
                {
                    "[MatchmakingModes]",
                    "0=1v1",
                    "1=2v2v2v2",
                    "",
                    "[1v1]",
                    "MaxPlayers=2",
                    "AlliedSides=Allies,Allied",
                    "SovietSides=Soviets,Soviet",
                    "AlliedColors=Blue,Green,Cyan,Yellow",
                    "SovietColors=Red,Orange,Purple,Pink",
                    "chkShortGame=True",
                    "chkRedeplMCV=True",
                    "chkAutoRepair=False",
                    "chkMultiEng=False",
                    "chkIngameAllying=True",
                    "chkDestrBridges=True",
                    "chkBuildOffAlly=True",
                    "chkCrates=False",
                    "chkDisableGameSpeed=True",
                    "chkSuperWeapons=False",
                    "chkNoYuri=True",
                    "cmbCredits=10000",
                    "cmbStartingUnits=0",
                    "cmbGameSpeedCapMultiplayer=0",
                    "AssignTeams=False",
                    "",
                    "[2v2v2v2]",
                    "MaxPlayers=8",
                    "AlliedSides=Allies,Allied",
                    "SovietSides=Soviets,Soviet",
                    "AlliedColors=Blue,Green,Cyan,Yellow",
                    "SovietColors=Red,Orange,Purple,Pink",
                    "chkShortGame=True",
                    "chkRedeplMCV=True",
                    "chkAutoRepair=False",
                    "chkMultiEng=False",
                    "chkIngameAllying=True",
                    "chkDestrBridges=True",
                    "chkBuildOffAlly=True",
                    "chkCrates=False",
                    "chkDisableGameSpeed=True",
                    "chkSuperWeapons=False",
                    "chkNoYuri=True",
                    "cmbCredits=10000",
                    "cmbStartingUnits=0",
                    "cmbGameSpeedCapMultiplayer=0",
                    "AssignTeams=True"
                };
                System.IO.File.WriteAllLines(path, lines);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to create default Matchmaking.ini: " + ex.Message);
            }
        }
    }
}

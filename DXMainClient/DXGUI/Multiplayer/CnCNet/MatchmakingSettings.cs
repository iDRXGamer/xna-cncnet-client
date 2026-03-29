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

        public List<MatchmakingModeDefinition> Modes { get; private set; }

        private MatchmakingSettings()
        {
            Modes = new List<MatchmakingModeDefinition>();
        }

        public void Initialize()
        {
            Modes.Clear();
            
            // 1. 1v1 Mode
            var mode1v1 = new MatchmakingModeDefinition
            {
                UIName = "1v1",
                PlayerCount = 2,
                AlliedSideNames = new[] { "Allies", "Allied" },
                SovietSideNames = new[] { "Soviets", "Soviet" },
                AlliedColors = new[] { "Blue", "Green", "Cyan", "Yellow" },
                SovietColors = new[] { "Red", "Orange", "Purple", "Pink" },
                AssignTeams = false
            };
            AddStandardForces(mode1v1);
            Modes.Add(mode1v1);

            // 2. 2v2 Mode
            var mode2v2 = new MatchmakingModeDefinition
            {
                UIName = "2v2",
                PlayerCount = 4,
                AlliedSideNames = new[] { "Allies", "Allied" },
                SovietSideNames = new[] { "Soviets", "Soviet" },
                AlliedColors = new[] { "Blue", "Green", "Cyan", "Yellow" },
                SovietColors = new[] { "Red", "Orange", "Purple", "Pink" },
                AssignTeams = true
            };
            AddStandardForces(mode2v2);
            Modes.Add(mode2v2);

            // 3. 2v2v2v2 Mode
            var mode2v2v2v2 = new MatchmakingModeDefinition
            {
                UIName = "2v2v2v2",
                PlayerCount = 8,
                AlliedSideNames = new[] { "Allies", "Allied" },
                SovietSideNames = new[] { "Soviets", "Soviet" },
                AlliedColors = new[] { "Blue", "Green", "Cyan", "Yellow" },
                SovietColors = new[] { "Red", "Orange", "Purple", "Pink" },
                AssignTeams = true
            };
            AddStandardForces(mode2v2v2v2);
            Modes.Add(mode2v2v2v2);
        }

        private void AddStandardForces(MatchmakingModeDefinition mode)
        {
            mode.ForceCheckboxes["chkShortGame"] = true;
            mode.ForceCheckboxes["chkRedeplMCV"] = true;
            mode.ForceCheckboxes["chkAutoRepair"] = false;
            mode.ForceCheckboxes["chkMultiEng"] = false;
            mode.ForceCheckboxes["chkIngameAllying"] = true;
            mode.ForceCheckboxes["chkDestrBridges"] = true;
            mode.ForceCheckboxes["chkBuildOffAlly"] = true;
            mode.ForceCheckboxes["chkCrates"] = false;
            mode.ForceCheckboxes["chkDisableGameSpeed"] = true;
            mode.ForceCheckboxes["chkSuperWeapons"] = false;
            mode.ForceCheckboxes["chkNoYuri"] = true;

            mode.ForceDropdowns["cmbCredits"] = "10000";
            mode.ForceDropdowns["cmbStartingUnits"] = "0";
            mode.ForceDropdowns["cmbGameSpeedCapMultiplayer"] = "0";
        }
    }
}

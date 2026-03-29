#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Rampastring.Tools;
using ClientCore;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class MatchmakingMapDefinitions
    {
        private static MatchmakingMapDefinitions? instance;

        public static MatchmakingMapDefinitions Instance => instance ??= new MatchmakingMapDefinitions();

        public Dictionary<string, List<string>> ModeMaps { get; private set; }

        private MatchmakingMapDefinitions()
        {
            ModeMaps = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public void Initialize()
        {
            ModeMaps.Clear();
            
            // 1v1 Maps
            ModeMaps["1v1"] = new List<string> { "Blood Feud", "May Day", "Dry Heat", "Arena Valley Extreme" };

            // 2v2 Maps
            ModeMaps["2v2"] = new List<string> { "Heck Freezes Over", "Tournament A" };

            // 2v2v2v2 Maps
            ModeMaps["2v2v2v2"] = new List<string> { "Invasion", "Snow Valley" };
        }
    }
}

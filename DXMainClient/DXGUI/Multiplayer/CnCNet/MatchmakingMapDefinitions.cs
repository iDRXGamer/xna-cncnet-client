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

        public Dictionary<string, List<string>> ModeMapHashes { get; private set; }

        private MatchmakingMapDefinitions()
        {
            ModeMapHashes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public void Initialize()
        {
            ModeMapHashes.Clear();
            
            // 1v1 Map Hashes
            ModeMapHashes["1v1"] = new List<string> { "<SHA1_BLOOD_FEUD>", "<SHA1_MAY_DAY>", "<SHA1_DRY_HEAT>", "<SHA1_ARENA_VALLEY>" };

            // 2v2 Map Hashes
            ModeMapHashes["2v2"] = new List<string> { "<SHA1_HECK_FREEZES>", "<SHA1_TOURNAMENT_A>" };

            // 2v2v2v2 Map Hashes
            ModeMapHashes["2v2v2v2"] = new List<string> { "<SHA1_INVASION>", "<SHA1_SNOW_VALLEY>" };
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class MatchmakingMapEntry
    {
        public string SHA1 { get; set; } = string.Empty;

        /// <summary>
        /// Map of Team ID (1=A, 2=B, etc.) to list of allowed Spawn Point IDs.
        /// </summary>
        public Dictionary<int, int[]> TeamSpawns { get; set; } = new Dictionary<int, int[]>();

        public string? GameMode { get; set; }

        public MatchmakingMapEntry(string sha1)
        {
            SHA1 = sha1;
        }

        public bool HasForcedSpawns => TeamSpawns.Count > 0;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Rampastring.Tools;
using ClientCore;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class MatchmakingMapDefinitions
    {
        private static MatchmakingMapDefinitions instance;
        public static MatchmakingMapDefinitions Instance => instance ?? (instance = new MatchmakingMapDefinitions());

        public Dictionary<string, List<string>> ModeMaps { get; private set; }

        private MatchmakingMapDefinitions()
        {
            ModeMaps = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public void Initialize()
        {
            ModeMaps.Clear();
            string iniPath = ProgramConstants.GamePath + "INI/MatchmakingMaps.ini";
            if (!System.IO.File.Exists(iniPath))
            {
                CreateDefaultMaps(iniPath);
            }

            IniFile ini = new IniFile(iniPath);
            foreach (var section in ini.GetSections())
            {
                if (string.IsNullOrEmpty(section)) continue;
                
                var keys = ini.GetSectionKeys(section);
                if (keys == null) continue;

                var maps = new List<string>();
                foreach (string key in keys)
                {
                    string mapName = ini.GetStringValue(section, key, string.Empty);
                    if (!string.IsNullOrWhiteSpace(mapName))
                    {
                        maps.Add(mapName.Trim());
                    }
                }
                ModeMaps[section] = maps;
            }
        }

        private void CreateDefaultMaps(string path)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                var lines = new List<string>
                {
                    "[1v1]",
                    "Map1=Dry Heat",
                    "Map2=Arena Valley Extreme",
                    "",
                    "[2v2v2v2]",
                    "Map1=Heck Freezes Over",
                    "Map2=Snow Valley"
                };
                System.IO.File.WriteAllLines(path, lines);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to create default MatchmakingMaps.ini: " + ex.Message);
            }
        }
    }
}

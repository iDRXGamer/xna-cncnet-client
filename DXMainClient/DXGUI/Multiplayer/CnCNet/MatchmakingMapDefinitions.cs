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
            
            string iniPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "INI", "MatchmakingMaps.ini");
            var fileInfo = SafePath.GetFile(iniPath);
            if (!fileInfo.Exists)
            {
                Logger.Log($"[Matchmaking] Warning: Configuration file not found at {iniPath}. Map pools will be empty.");
                return;
            }

            IniFile ini = new IniFile(iniPath);
            var sections = ini.GetSections();
            
            if (sections == null || sections.Count == 0)
            {
                Logger.Log($"[Matchmaking] Warning: No sections defined in {iniPath}. Map pools will be empty.");
                return;
            }

            foreach (string section in sections)
            {
                var keys = ini.GetSectionKeys(section);
                if (keys != null && keys.Count > 0)
                {
                    List<string> mapHashes = new List<string>();
                    foreach (string key in keys)
                    {
                        string hash = ini.GetStringValue(section, key, string.Empty);
                        if (!string.IsNullOrEmpty(hash))
                        {
                            mapHashes.Add(hash);
                        }
                    }
                    if (mapHashes.Count > 0)
                    {
                        ModeMapHashes[section] = mapHashes;
                        Logger.Log($"[Matchmaking] Loaded map pool for mode [{section}] with {mapHashes.Count} maps.");
                    }
                }
            }
        }
    }
}

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

        public Dictionary<string, List<MatchmakingMapEntry>> ModeMapEntries { get; private set; }

        private MatchmakingMapDefinitions()
        {
            ModeMapEntries = new Dictionary<string, List<MatchmakingMapEntry>>(StringComparer.OrdinalIgnoreCase);
        }

        public void Initialize()
        {
            ModeMapEntries.Clear();
            
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
                    List<MatchmakingMapEntry> entries = new List<MatchmakingMapEntry>();
                    foreach (string key in keys)
                    {
                        string rawValue = ini.GetStringValue(section, key, string.Empty);
                        if (string.IsNullOrEmpty(rawValue))
                            continue;

                        // Format: SHA1|TeamA:1,2|TeamB:3,4
                        string[] parts = rawValue.Split('|');
                        var entry = new MatchmakingMapEntry(parts[0].Trim());

                        for (int i = 1; i < parts.Length; i++)
                        {
                            string tag = parts[i].Trim();
                            if (tag.StartsWith("Team", StringComparison.OrdinalIgnoreCase))
                            {
                                int colonIndex = tag.IndexOf(':');
                                if (colonIndex > 0)
                                {
                                    string teamName = tag.Substring(0, colonIndex).ToUpper();
                                    string spawnList = tag.Substring(colonIndex + 1);

                                    int teamId = teamName switch
                                    {
                                        "TEAMA" => 1,
                                        "TEAMB" => 2,
                                        "TEAMC" => 3,
                                        "TEAMD" => 4,
                                        _ => 0
                                    };

                                    if (teamId > 0)
                                    {
                                        entry.TeamSpawns[teamId] = spawnList.Split(',')
                                            .Select(s => int.TryParse(s.Trim(), out int val) ? val : -1)
                                            .Where(v => v >= 0)
                                            .ToArray();
                                    }
                                }
                            }
                        }
                        Logger.Log($"[Matchmaking] Loaded map entry for SHA1 {entry.SHA1} with {entry.TeamSpawns.Count} team mappings from raw: {rawValue}");
                        entries.Add(entry);
                    }
                    if (entries.Count > 0)
                    {
                        ModeMapEntries[section] = entries;
                        Logger.Log($"[Matchmaking] Loaded map pool for mode [{section}] with {entries.Count} maps.");
                    }
                }
            }
        }
    }
}

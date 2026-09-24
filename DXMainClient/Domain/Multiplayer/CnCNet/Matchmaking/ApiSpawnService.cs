#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using ClientCore;

using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.Matchmaking
{
    /// <summary>
    /// Writes spawn.ini and spawnmap.ini for a match found by casual matchmaking.
    /// All game settings come from the server, so every player writes the same settings.
    /// </summary>
    public class ApiSpawnService
    {
        private const int BASE_GAME_PORT = 50000;

        private readonly MapLoader mapLoader;

        public ApiSpawnService(MapLoader mapLoader)
        {
            this.mapLoader = mapLoader;
        }

        /// <summary>
        /// Writes the spawn files for a "spawn" response.
        /// Returns false if the files could not be written, in which case the game must not be started.
        /// </summary>
        public bool WriteSpawnFiles(QmMatchResponse spawnResponse)
        {
            if (spawnResponse.Spawn == null)
            {
                Logger.Log("[Matchmaking] The spawn response does not contain spawn settings.");
                return false;
            }

            try
            {
                var spawnIni = new IniFile();
                WriteServerValues(spawnIni, spawnResponse.Spawn);

                string mapHash = spawnIni.GetStringValue("Settings", "MapHash", string.Empty);
                Map? map = mapLoader.GameModes.SelectMany(gm => gm.Maps)
                    .FirstOrDefault(m => string.Equals(m.SHA1, mapHash, StringComparison.OrdinalIgnoreCase));

                if (map == null)
                {
                    Logger.Log($"[Matchmaking] The match map is not installed (SHA1 '{mapHash}'), the match cannot be started.");
                    return false;
                }

                spawnIni.SetStringValue("Settings", "Scenario", ProgramConstants.SPAWNMAP_INI);

                int myIndex = spawnIni.GetIntValue("Settings", "MyIndex", 0);
                spawnIni.SetStringValue("Settings", "Host", myIndex == 0 ? "Yes" : "No");

                if (spawnIni.GetIntValue("Settings", "Port", 0) <= 0)
                    spawnIni.SetIntValue("Settings", "Port", BASE_GAME_PORT + myIndex);

                // Other players need a Host flag, a port and an address; 0.0.0.0 makes the spawner use the tunnel
                foreach (string otherSection in spawnIni.GetSections().Where(s => s.StartsWith("Other", StringComparison.Ordinal)))
                {
                    int otherIndex = spawnIni.GetIntValue(otherSection, "MyIndex", 0);
                    spawnIni.SetStringValue(otherSection, "Host", otherIndex == 0 ? "Yes" : "No");

                    if (string.IsNullOrWhiteSpace(spawnIni.GetStringValue(otherSection, "Ip", string.Empty)))
                        spawnIni.SetStringValue(otherSection, "Ip", "0.0.0.0");

                    if (spawnIni.GetIntValue(otherSection, "Port", 0) <= 0)
                        spawnIni.SetIntValue(otherSection, "Port", BASE_GAME_PORT + otherIndex);
                }

                IniFile mapIni = map.GetMapIni();
                if (spawnResponse.SpawnMap != null)
                    WriteServerValues(mapIni, spawnResponse.SpawnMap);

                SafePath.DeleteFileIfExists(ProgramConstants.GamePath, ProgramConstants.SPAWNER_SETTINGS);
                SafePath.DeleteFileIfExists(ProgramConstants.GamePath, ProgramConstants.SPAWNMAP_INI);

                spawnIni.WriteIniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, ProgramConstants.SPAWNER_SETTINGS));
                mapIni.WriteIniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, ProgramConstants.SPAWNMAP_INI));

                Logger.Log($"[Matchmaking] Wrote spawn files for map {map.Name}.");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Matchmaking] Failed to write spawn files: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Copies server-provided sections into an INI file. The values contain data from
        /// other players (e.g. their names), so line breaks and section brackets are removed
        /// to prevent them from injecting additional sections or keys into the file.
        /// </summary>
        private static void WriteServerValues(IniFile iniFile, Dictionary<string, Dictionary<string, object>> sections)
        {
            foreach (KeyValuePair<string, Dictionary<string, object>> section in sections)
            {
                string sectionName = SanitizeIniName(section.Key);

                foreach (KeyValuePair<string, object> kvp in section.Value)
                {
                    if (kvp.Value == null)
                        continue;

                    string value = RemoveLineBreaks(kvp.Value.ToString() ?? string.Empty);
                    iniFile.SetStringValue(sectionName, SanitizeIniName(kvp.Key), value);
                }
            }
        }

        private static string SanitizeIniName(string name)
            => RemoveLineBreaks(name).Replace("[", string.Empty).Replace("]", string.Empty).Replace("=", string.Empty);

        private static string RemoveLineBreaks(string text)
            => text.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}

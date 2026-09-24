#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DTAClient.Domain.Multiplayer.CnCNet.Matchmaking
{
    public class QmMatchRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("side")]
        public int Side { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("client_version")]
        public string? ClientVersion { get; set; }

        [JsonPropertyName("ip_port")]
        public int? IpPort { get; set; }

        /// <summary>
        /// Random token that is sent with every request of a search. The server only lets the client
        /// that started a search continue or cancel it, since casual players do not log in.
        /// </summary>
        [JsonPropertyName("search_token")]
        public string? SearchToken { get; set; }
    }

    public class QmMatchResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("checkback")]
        public int CheckBack { get; set; }

        [JsonPropertyName("gameID")]
        public long? GameID { get; set; }

        [JsonPropertyName("spawn")]
        public Dictionary<string, Dictionary<string, object>>? Spawn { get; set; }

        [JsonPropertyName("spawnmap")]
        public Dictionary<string, Dictionary<string, object>>? SpawnMap { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}

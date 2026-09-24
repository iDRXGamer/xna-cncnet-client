#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.Matchmaking
{
    /// <summary>
    /// Client for the casual matchmaking endpoints of the CnCNet ladder API.
    /// </summary>
    public sealed class MatchmakingApiService : IDisposable
    {
        private const int REQUEST_TIMEOUT_SECONDS = 15;
        private const int MAX_RESPONSE_SIZE_BYTES = 1024 * 1024;

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient httpClient;
        private readonly string apiBaseUrl;

        public MatchmakingApiService(string apiBaseUrl)
        {
            this.apiBaseUrl = NormalizeApiBaseUrl(apiBaseUrl);

            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS),
                MaxResponseContentBufferSize = MAX_RESPONSE_SIZE_BYTES
            };

            // Without this the API answers authentication and validation errors with a redirect
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private static string NormalizeApiBaseUrl(string url)
        {
            string trimmedUrl = url.TrimEnd('/');

            if (trimmedUrl.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
                return trimmedUrl;

            if (trimmedUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                return trimmedUrl + "/v1";

            return trimmedUrl + "/api/v1";
        }

        /// <summary>
        /// Sends a casual matchmaking request. Never throws; network and server errors
        /// are returned as a response of type "error".
        /// </summary>
        public async Task<QmMatchResponse> SendMatchRequestAsync(string ladder, string playerName, QmMatchRequest request)
        {
            string url = $"{apiBaseUrl}/qm/casual/{Uri.EscapeDataString(ladder)}/{Uri.EscapeDataString(playerName)}";

            try
            {
                string json = JsonSerializer.Serialize(request, jsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await httpClient.PostAsync(url, content).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log($"[Matchmaking] HTTP {(int)response.StatusCode} for '{request.Type}' request: {responseBody}");
                    return CreateErrorResponse($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                QmMatchResponse? result = JsonSerializer.Deserialize<QmMatchResponse>(responseBody, jsonOptions);
                if (result == null || string.IsNullOrEmpty(result.Type))
                {
                    Logger.Log($"[Matchmaking] Unexpected response for '{request.Type}' request: {responseBody}");
                    return CreateErrorResponse("Unexpected response from the matchmaking server");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Matchmaking] '{request.Type}' request failed: {ex.GetType().Name}: {ex.Message}");
                return CreateErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Gets the number of players waiting in each casual ladder, or null if the request failed.
        /// </summary>
        public async Task<Dictionary<string, int>?> GetQueueCountsAsync()
        {
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync($"{apiBaseUrl}/qm/casual/queue-counts").ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return JsonSerializer.Deserialize<Dictionary<string, int>>(responseBody, jsonOptions);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Matchmaking] Queue counts request failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the names of the maps of a ladder's map pool, or null if the request failed.
        /// The map pool is set on the ladder API, so it is the same list that ranked quick match shows.
        /// </summary>
        public async Task<List<string>?> GetMapNamesAsync(string ladder)
        {
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync($"{apiBaseUrl}/qm/ladder/{Uri.EscapeDataString(ladder)}/maps/public").ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                List<LadderMap>? maps = JsonSerializer.Deserialize<List<LadderMap>>(responseBody, jsonOptions);

                return maps?.Select(map => map.Description).OfType<string>().ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"[Matchmaking] Map pool request for ladder {ladder} failed: {ex.Message}");
                return null;
            }
        }

        private sealed class LadderMap
        {
            public string? Description { get; set; }
        }

        private static QmMatchResponse CreateErrorResponse(string description)
            => new QmMatchResponse { Type = "error", Description = description };

        public void Dispose() => httpClient.Dispose();
    }
}

#nullable enable

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.Matchmaking
{
    public class MatchmakingApiService : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly string apiBaseUrl;
        private readonly JsonSerializerOptions jsonOptions;

        public MatchmakingApiService(string apiBaseUrl)
        {
            this.apiBaseUrl = NormalizeApiBaseUrl(apiBaseUrl);
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        private static string NormalizeApiBaseUrl(string url)
        {
            string clean = url.TrimEnd('/');

            if (!clean.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
            {
                if (clean.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    clean += "/v1";
                }
                else
                {
                    clean += "/api/v1";
                }
            }

            return clean;
        }

        public async Task<QmMatchResponse?> SendMatchRequestAsync(string ladder, string playerName, QmMatchRequest request)
        {
            string url = $"{apiBaseUrl}/qm/{Uri.EscapeDataString(ladder)}/{Uri.EscapeDataString(playerName)}";
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                string json = JsonSerializer.Serialize(request, jsonOptions);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                Logger.Log($"[MatchmakingApi] Sending '{request.Type}' for '{playerName}' to {url}");

                HttpResponseMessage response = await httpClient.PostAsync(url, content);
                stopwatch.Stop();

                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log($"[MatchmakingApi] HTTP error {response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms: {responseBody}");

                    return new QmMatchResponse
                    {
                        Type = "error",
                        Description = $"Server error (HTTP {(int)response.StatusCode}): {response.ReasonPhrase}"
                    };
                }

                QmMatchResponse? result = JsonSerializer.Deserialize<QmMatchResponse>(responseBody, jsonOptions);

                if (result == null)
                {
                    Logger.Log($"[MatchmakingApi] Empty JSON response from server in {stopwatch.ElapsedMilliseconds}ms: {responseBody}");

                    return new QmMatchResponse
                    {
                        Type = "error",
                        Description = "Empty response received from matchmaking server."
                    };
                }

                Logger.Log($"[MatchmakingApi] Response received in {stopwatch.ElapsedMilliseconds}ms: type='{result.Type}'");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Log($"[MatchmakingApi] Request exception after {stopwatch.ElapsedMilliseconds}ms: {ex.GetType().Name}: {ex.Message}");

                return new QmMatchResponse
                {
                    Type = "error",
                    Description = ex.Message
                };
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}

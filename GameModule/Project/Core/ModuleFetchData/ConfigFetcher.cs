using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using GameModule.Authentication;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    public class ConfigFetcher
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly AuthenticationModule _authModule;
        private readonly GameState _gameState;

        public ConfigFetcher(ILogger<ConfigFetcher> logger, AuthenticationModule authModule, GameState gameState)
        {
            _logger = logger;
            _authModule = authModule;
            _gameState = gameState;
        }

        public async Task<Dictionary<string, string>> FetchAdminConfigs(IExecutionContext context)
        {
            try
            {
                // 1. Get the Stateless Admin Token
                // Note: Passing 'null' for GameState assumes keys are handled internally or not needed for this flow yet. 
                // Adjust if your GetStatelessToken strictily requires GameState.
                string adminToken = await _authModule.GetBase64EncodedApiKey(context, _gameState);

                // 2. Build the Admin API URL
                // Ref: https://services.api.unity.com/remote-config/v1/projects/<projectId>/environments/<environmentId>/configs
                string url = $"https://services.api.unity.com/remote-config/v1/projects/{context.ProjectId}/environments/{context.EnvironmentId}/configs";

                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", adminToken);

                    using (HttpResponseMessage response = await _httpClient.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string errorBody = await response.Content.ReadAsStringAsync();
                            throw new Exception($"Admin Config Fetch Failed: {response.StatusCode} - {errorBody}");
                        }

                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        return ParseAdminResponse(jsonResponse);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ConfigFetcher] Failed to fetch admin configs.");
                throw;
            }
        }

        private Dictionary<string, string> ParseAdminResponse(string json)
        {
            AdminConfigRoot? root = JsonConvert.DeserializeObject<AdminConfigRoot>(json);
            Dictionary<string, string> result = new Dictionary<string, string>();

            if (root?.Configs == null)
            {
                return result;
            }

            // Iterate through the configs array to find the "settings" type
            foreach (AdminConfigEntry config in root.Configs)
            {
                if (config.Type == "settings" && config.Value != null)
                {
                    foreach (AdminConfigValue item in config.Value)
                    {
                        // Safely convert value to string
                        result[item.Key] = item.Value?.ToString() ?? string.Empty;
                    }
                }
            }

            return result;
        }

        // --- JSON Data Structures for Admin API ---
        
        private class AdminConfigRoot
        {
            [JsonProperty("configs")]
            public List<AdminConfigEntry> Configs { get; set; }
        }

        private class AdminConfigEntry
        {
            [JsonProperty("type")]
            public string Type { get; set; } // e.g. "settings"

            [JsonProperty("value")]
            public List<AdminConfigValue> Value { get; set; }
        }

        private class AdminConfigValue
        {
            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("value")]
            public object Value { get; set; }
            
            [JsonProperty("type")]
            public string Type { get; set; }
        }
    }
}
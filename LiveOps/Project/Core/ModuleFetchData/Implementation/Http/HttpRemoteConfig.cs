using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.GameModule;
using GameModuleDTO.Json;

namespace GameModule.ModuleFetchData.Http
{
    /// <summary>
    /// Fetches remote configurations using a basic HTTP GET request.
    /// </summary>
    public class HttpRemoteConfig : IRemoteConfig
    {
        private readonly ILogger<HttpRemoteConfig> _logger;
        private readonly HttpClient _httpClient;

        private Dictionary<string, string> _cache = new Dictionary<string, string>();
        private bool _isFetched = false;
        private string _configUrl = "https://script.googleusercontent.com/macros/echo?user_content_key=AY5xjrTm5V4rzA7reMpYHmZhdkdA0wH-WKF-YzYtJbA-jMndHRJ7hxgBIVWrQaACrJkrpAsjNKVeGjmHGn6ssa0x5kCbP6PU_2j7KgdmF8BuppiQry8aHHNSKvHWUZ8KjcTF-2s9QgF7zfujnEbA5DxfCwKGhu04_6k43LZ8y9n1-pyAaEUYeesAkbhEqKmnvQe5Fuu0O1iTEmCFSTTLKxjz6upUMKCQR17li53PTw-lRFhq19pHPNiwsC__wXkQB2nOrKptWi-8F3rQ61SUn9YUrJn7lmWrutjefaVgnwCdvmy5yEpeSINtPX8Wfkvi9CrZVo1V2rvcydoHEU8ncX6PJZ76BItIo-xSl9ZeeqOm&lib=MvkPUsZBzdDehtZWzqKcfcf-qERZM9JSA"; // Placeholder URL
        private string _specialKey = "entities";

        public HttpRemoteConfig(ILogger<HttpRemoteConfig> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public void SetConfigUrl(string url)
        {
            _configUrl = url;
            _isFetched = false; // Reset if URL changes
        }

        public void SetSpecialKey(string specialKey)
        {
            _specialKey = specialKey;
            _isFetched = false; // Reset if key changes
        }

        private async Task FetchIfNeeded(IExecutionContext context, string specialKey = "")
        {
            if (_isFetched)
            {
                return;
            }

            _logger.LogInformation("[HttpRemoteConfig] Initializing fetch from {Url} with SpecialKey '{SpecialKey}'", _configUrl, specialKey);

            Dictionary<string, string>? remoteData = null;
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(_configUrl);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                JObject root = JObject.Parse(json);
                JToken? dataToken = root;

                if (!string.IsNullOrEmpty(specialKey))
                {
                    if (root.TryGetValue(specialKey, StringComparison.OrdinalIgnoreCase, out JToken? value))
                    {
                        dataToken = value;
                    }
                    else
                    {
                        _logger.LogWarning("[HttpRemoteConfig] SpecialKey '{SpecialKey}' not found in JSON response.", specialKey);
                        dataToken = null;
                    }
                }

                if (dataToken != null)
                {
                    if (dataToken is JObject jObj)
                    {
                        remoteData = jObj.Properties().ToDictionary(
                            p => p.Name,
                            p => p.Value.Type == JTokenType.String ? p.Value.ToString() : p.Value.ToString(Formatting.None)
                        );
                    }
                    else if (dataToken is JArray jArray)
                    {
                        remoteData = new Dictionary<string, string>();
                        foreach (JToken item in jArray)
                        {
                            if (item is JObject itemObj)
                            {
                                // Robust key extraction: key property -> id (if string and not "key") -> name -> id fallback
                                string? extractionKey = null;
                                if (itemObj["key"] != null)
                                {
                                    extractionKey = itemObj["key"]!.ToString();
                                }
                                else if (itemObj["id"] != null)
                                {
                                    JToken idToken = itemObj["id"]!;
                                    if (idToken.Type == JTokenType.String && idToken.ToString() != "key")
                                    {
                                        extractionKey = idToken.ToString();
                                    }
                                    else if (itemObj["name"] != null)
                                    {
                                        extractionKey = itemObj["name"]!.ToString();
                                    }
                                    else
                                    {
                                        extractionKey = idToken.ToString();
                                    }
                                }
                                else if (itemObj["name"] != null)
                                {
                                    extractionKey = itemObj["name"]!.ToString();
                                }

                                if (string.IsNullOrEmpty(extractionKey) || extractionKey == "key") continue;

                                // Robust value extraction: value property -> number -> plainJson -> whole item fallback
                                JToken valToken = itemObj["value"] ?? itemObj["number"] ?? itemObj["plainJson"] ?? itemObj;
                                remoteData[extractionKey] = valToken.Type == JTokenType.String 
                                    ? valToken.ToString() 
                                    : valToken.ToString(Formatting.None);
                            }
                        }
                    }
                }

                if (remoteData != null)
                {
                    _logger.LogInformation("[HttpRemoteConfig] Remote fetch successful. Received {Count} keys.", remoteData.Count);
                }
                else
                {
                    _logger.LogWarning("[HttpRemoteConfig] Remote fetch returned empty or null data.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HttpRemoteConfig] Remote fetch failed.");
            }

            if (remoteData == null)
            {
                _logger.LogInformation("[HttpRemoteConfig] No remote data; cache is empty.");
                _cache = new Dictionary<string, string>();
            }
            else
            {
                _cache = remoteData;
            }

            _logger.LogInformation("[HttpRemoteConfig] Initialization complete. Final cache size: {Count} json: {Json}", _cache.Count, JsonConvert.SerializeObject(_cache));
            _isFetched = true;
        }

        #region IRemoteConfig Implementation
        public async Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            await FetchIfNeeded(context, _specialKey);
            if (_cache.TryGetValue(key, out string value))
            {
                try { return value.FromJson<T>(); } catch { return defaultValue; }
            }
            return defaultValue;
        }

        public async Task<T> Get<T>(IExecutionContext context, T defaultValue) where T : IGameModuleData
        {
            return await Get(context, typeof(T).Name, defaultValue);
        }

        public async Task<Dictionary<string, T>> GetAllValues<T>(IExecutionContext context)
        {
            await FetchIfNeeded(context, _specialKey);
            var results = new Dictionary<string, T>();
            foreach (var kvp in _cache)
            {
                try { results[kvp.Key] = kvp.Value.FromJson<T>(); } catch { }
            }
            return results;
        }

        public async Task<string> GetRaw(IExecutionContext context, string key)
        {
            await FetchIfNeeded(context, _specialKey);
            return _cache.TryGetValue(key, out string value) ? value : string.Empty;
        }

        public async Task<bool> Exists(IExecutionContext context, string key)
        {
            await FetchIfNeeded(context, _specialKey);
            return _cache.ContainsKey(key);
        }
        #endregion
    }
}

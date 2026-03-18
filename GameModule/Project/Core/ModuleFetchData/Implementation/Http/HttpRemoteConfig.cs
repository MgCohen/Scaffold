using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.GameModule;
using GameModuleDTO.Json;

namespace GameModule.ModuleFetchData.Http
{
    /// <summary>
    /// Fetches remote configurations using a basic HTTP GET request.
    /// Uses LocalConfigProvider via composition for local JSON fallback.
    /// </summary>
    public class HttpRemoteConfig : IRemoteConfig
    {
        private readonly ILogger<HttpRemoteConfig> _logger;
        private readonly HttpClient _httpClient;
        private readonly LocalConfigProvider _localConfigProvider;
        
        private Dictionary<string, string> _cache = new Dictionary<string, string>();
        private bool _isFetched = false;
        private string _configUrl = "https://script.googleusercontent.com/macros/echo?..."; // Placeholder URL

        public HttpRemoteConfig(ILogger<HttpRemoteConfig> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _localConfigProvider = new LocalConfigProvider(logger);
        }

        public void SetConfigUrl(string url)
        {
            _configUrl = url;
        }

        private async Task FetchIfNeeded(IExecutionContext context)
        {
            if (_isFetched) return;

            Dictionary<string, string>? remoteData = null;
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(_configUrl);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                Dictionary<string, object>? data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (data != null)
                {
                    remoteData = data.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.ToString() ?? string.Empty
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HttpRemoteConfig] Fetch failed. Using local fallback.");
            }

            Dictionary<string, string> localData = _localConfigProvider.FetchLocalConfigs();
            
            if (remoteData == null)
            {
                _cache = localData;
            }
            else
            {
                _cache = remoteData;
                _localConfigProvider.Merge(_cache, localData);
            }

            _isFetched = true;
        }

        #region IRemoteConfig Implementation
        public async Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            await FetchIfNeeded(context);
            if (_cache.TryGetValue(key, out string value))
            {
                try { return value.FromJson<T>(); } catch { return defaultValue; }
            }
            return defaultValue;
        }

        public async Task<T> Get<T>(IExecutionContext context, T defaultValue) where T : IGameModuleData
        {
            return await Get(context, GameDataExtensions.GetKey<T>(), defaultValue);
        }

        public async Task<Dictionary<string, T>> GetAllValues<T>(IExecutionContext context)
        {
            await FetchIfNeeded(context);
            var results = new Dictionary<string, T>();
            foreach (var kvp in _cache)
            {
                try { results[kvp.Key] = kvp.Value.FromJson<T>(); } catch { }
            }
            return results;
        }

        public async Task<string> GetRaw(IExecutionContext context, string key)
        {
            await FetchIfNeeded(context);
            return _cache.TryGetValue(key, out string value) ? value : string.Empty;
        }

        public async Task<bool> Exists(IExecutionContext context, string key)
        {
            await FetchIfNeeded(context);
            return _cache.ContainsKey(key);
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.GameModule;

namespace GameModule.ModuleFetchData.Http
{
    /// <summary>
    /// Fetches remote configurations using a basic HTTP GET request.
    /// </summary>
    public class HttpRemoteConfig : IRemoteConfig
    {
        private readonly ILogger<HttpRemoteConfig> _logger;
        private readonly HttpClient _httpClient;

        // Caching locally fetched data
        private Dictionary<string, object> _cache = new Dictionary<string, object>();
        private bool _isFetched = false;
        private string _configUrl = "https://script.googleusercontent.com/macros/echo?user_content_key=AWDtjMUjq9_1oDbExHAJBNYPSvrYcdnF5g9qZ466VkjNd8BD4Y133XW4-byG_3P5jjnZM8f22J24vxs-KmhC5khcX8eLufYQ50eKya9Np3NJKyVgwFItr8Q1JuIVlmSLtU9FVjFhBPg3nO337peKpDylWk_mjuNy4bJJsT2hNyqKVsBZ6_iktSPT2ab1X5AB3kjCFZNJ_jezs-INQU53YVmSNOVSNfxbkEa0-yTNXLGGmF2m6HQka9qHLYEEdo1j-2aagDJt-PZlS926pb4f2_2ckzAAUYmB3pk1InKpJzJsnXK06kxsJl3tCnXyj7wJQCl-ERBZ3ikKOACJtyXg90IeQS7zg2X0GELEJ55d7uSL&lib=MvkPUsZBzdDehtZWzqKcfcf-qERZM9JSA"; // Placeholder URL

        public HttpRemoteConfig(ILogger<HttpRemoteConfig> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public void SetConfigUrl(string url)
        {
            _configUrl = url;
        }

        private async Task FetchIfNeeded()
        {
            if (_isFetched) return;

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(_configUrl);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                Dictionary<string, object>? data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (data != null)
                {
                    _cache = data;
                }
                _isFetched = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch remote config via HTTP.");
            }
        }

        public async Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            await FetchIfNeeded();
            if (_cache.TryGetValue(key, out object value))
            {
                if (value is T typedValue) return typedValue;
                try
                {
                    return JsonConvert.DeserializeObject<T>(value.ToString());
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public async Task<T> Get<T>(IExecutionContext context, T defaultValue) where T : IGameModuleData
        {
            return await Get(context, GameDataExtensions.GetKey<T>(), defaultValue);
        }

        public async Task<Dictionary<string, T>> GetAllValues<T>(IExecutionContext context)
        {
            await FetchIfNeeded();
            Dictionary<string, T> results = new Dictionary<string, T>();
            foreach (KeyValuePair<string, object> kvp in _cache)
            {
                try
                {
                    if (kvp.Value is T typedValue)
                    {
                        results[kvp.Key] = typedValue;
                    }
                    else
                    {
                        T? val = JsonConvert.DeserializeObject<T>(kvp.Value?.ToString() ?? "");
                        if (val != null) results[kvp.Key] = val;
                    }
                }
                catch { /* ignore invalid formats */ }
            }
            return results;
        }

        public async Task<string> GetRaw(IExecutionContext context, string key)
        {
            await FetchIfNeeded();
            if (_cache.TryGetValue(key, out object value))
            {
                return value?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        public async Task<bool> Exists(IExecutionContext context, string key)
        {
            await FetchIfNeeded();
            return _cache.ContainsKey(key);
        }
    }
}

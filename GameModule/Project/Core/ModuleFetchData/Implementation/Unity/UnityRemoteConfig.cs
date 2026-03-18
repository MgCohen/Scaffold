using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.RemoteConfig.Model;
using Unity.Services.CloudSave.Model;

namespace GameModule.ModuleFetchData.Unity
{
    /// <summary>
    /// Connects to Remote Config to fetch server parameters.
    /// </summary>
    public class UnityRemoteConfig : UnityDataCache, IRemoteConfig
    {
        public static UnityRemoteConfig Instance { get; private set; }
        protected UnityConfigFetcher _configFetcher;
        protected LocalConfigProvider _localConfigProvider;

        public UnityRemoteConfig(ILogger<UnityDataCache> logger, IGameApiClient gameApiClient, UnityConfigFetcher configFetcher) : base(logger, gameApiClient)
        {
            Instance = this;
            _configFetcher = configFetcher;
            _localConfigProvider = new LocalConfigProvider(logger);
        }

        protected override async Task<Dictionary<string, string>> FetchData(IExecutionContext context)
        {
            Dictionary<string, string>? remoteData = null;
            try
            {
                // CASE A: Server/Trigger Context (No Player)
                if (string.IsNullOrEmpty(context.PlayerId))
                {
                    _logger.LogInformation("[UnityRemoteConfig] Fetching via Admin API (Server Context)...");
                    remoteData = await _configFetcher.FetchAdminConfigs(context);
                }
                else
                {
                    // Player Context (SDK)
                    _logger.LogInformation($"[UnityRemoteConfig] Fetching via SDK. PlayerId: {context.PlayerId}");
                    ApiResponse<SettingsDeliveryResponse> result = await _gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(context, context.AccessToken, context.ProjectId, context.EnvironmentId);

                    if (result.Data != null && result.Data.Configs != null && result.Data.Configs.Settings != null)
                    {
                        remoteData = result.Data.Configs.Settings.ToDictionary(
                            item => item.Key,
                            item => item.Value?.ToString() ?? string.Empty
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[UnityRemoteConfig] Fetch failed. Using local fallback.");
            }

            Dictionary<string, string> localData = _localConfigProvider.FetchLocalConfigs();

            if (remoteData == null)
            {
                _logger.LogInformation("[UnityRemoteConfig] No remote data found. Using {Count} local config entries.", localData.Count);
                return localData;
            }

            _localConfigProvider.Merge(remoteData, localData);
            return remoteData;
        }

        // --- Abstract Implementations ---
        protected override Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock) => throw new NotImplementedException();
        protected override Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock) => throw new NotImplementedException();
        protected override Task DeleteData(IExecutionContext context, string key) => throw new NotImplementedException();
    }
}
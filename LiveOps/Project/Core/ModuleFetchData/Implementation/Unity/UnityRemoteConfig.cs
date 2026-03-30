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
        public UnityRemoteConfig(ILogger<UnityDataCache> logger, IGameApiClient gameApiClient) : base(logger, gameApiClient)
        {

        }

        protected override async Task<Dictionary<string, string>> FetchData(IExecutionContext context)
        {
            Dictionary<string, string>? remoteData = null;
            try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[UnityRemoteConfig] Fetch failed.");
            }

            if (remoteData == null)
            {
                _logger.LogInformation("[UnityRemoteConfig] No remote data; returning empty config map.");
                return new Dictionary<string, string>();
            }

            return remoteData;
        }

        // --- Abstract Implementations ---
        protected override Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock) => throw new NotImplementedException();
        protected override Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock) => throw new NotImplementedException();
        protected override Task DeleteData(IExecutionContext context, string key) => throw new NotImplementedException();
    }
}
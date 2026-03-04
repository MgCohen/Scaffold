using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;
using Unity.Services.RemoteConfig.Model;

namespace GameModule.ModuleFetchData
{
    /// <summary>
    /// Connects to Remote Config to fetch server parameters.
    /// </summary>
    public class RemoteConfig : DataCache
    {
        public static RemoteConfig Instance { get; private set; }
        protected ConfigFetcher _configFetcher;

        public RemoteConfig(ILogger<DataCache> logger, IGameApiClient gameApiClient, ConfigFetcher configFetcher) : base(logger, gameApiClient)
        {
            Instance = this;
            _configFetcher = configFetcher;
        }

        protected override async Task<Dictionary<string, string>> FetchData(IExecutionContext context)
        {
            try
            {
                // CASE A: Server/Trigger Context (No Player)
                if (string.IsNullOrEmpty(context.PlayerId))
                {
                    _logger.LogInformation("[RemoteConfig] Fetching via Admin API (Server Context)...");
                    return await _configFetcher.FetchAdminConfigs(context);
                }

                // Player Context (SDK)
                _logger.LogInformation($"[RemoteConfig] Fetching via SDK. PlayerId: {context.PlayerId}");
                ApiResponse<SettingsDeliveryResponse> result = await _gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(context, context.AccessToken, context.ProjectId, context.EnvironmentId);

                if (result.Data == null || result.Data.Configs == null || result.Data.Configs.Settings == null)
                {
                    return new Dictionary<string, string>();
                }

                return result.Data.Configs.Settings.ToDictionary(
                    item => item.Key,
                    item => item.Value?.ToString() ?? string.Empty
                );
            }
            catch (ApiException apiEx)
            {
                _logger.LogError($"[RemoteConfig] API Error: {apiEx.Response.StatusCode} - {apiEx.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[RemoteConfig] General Error. PlayerId: '{context.PlayerId}'");
                throw;
            }
        }

        // --- Abstract Implementations ---
        protected override Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock) => throw new NotImplementedException();
        protected override Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock) => throw new NotImplementedException();
        protected override Task DeleteData(IExecutionContext context, string key) => throw new NotImplementedException();
    }
}

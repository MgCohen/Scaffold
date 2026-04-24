using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.RemoteConfig.Model;

namespace LiveOps.ModuleFetchData.Unity
{
    /// <summary>
    /// Fetches server Remote Config via the Unity services SDK (read-only; see <see cref="ReadonlyUnityDataCache" />).
    /// </summary>
    public class UnityRemoteConfig : ReadonlyUnityDataCache, IRemoteConfig
    {
        public UnityRemoteConfig(ILogger<UnityRemoteConfig> logger, IGameApiClient gameApiClient) : base(logger, gameApiClient)
        {
        }

        protected override async Task<Dictionary<string, string>> FetchData(IExecutionContext context)
        {
            Dictionary<string, string>? remoteData = null;
            try
            {
                _logger.LogDebug("[UnityRemoteConfig] Fetching via SDK. PlayerId: {PlayerId}", context.PlayerId);
                ApiResponse<SettingsDeliveryResponse> result = await _gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
                    context, context.AccessToken, context.ProjectId, context.EnvironmentId);

                if (result.Data?.Configs?.Settings is { } settings)
                {
                    remoteData = settings.ToDictionary(
                        item => item.Key,
                        item => item.Value?.ToString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[UnityRemoteConfig] Remote config fetch failed.");
            }

            if (remoteData is null)
            {
                _logger.LogDebug("[UnityRemoteConfig] No remote data; using empty config map.");
                return new Dictionary<string, string>();
            }

            return remoteData;
        }
    }
}

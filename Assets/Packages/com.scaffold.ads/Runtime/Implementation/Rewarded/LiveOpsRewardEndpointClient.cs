using System.Threading.Tasks;
using GameModuleDTO.ModuleRequests;
using Scaffold.LiveOps;
using UnityEngine;

namespace Scaffold.Ads
{
    public class LiveOpsRewardEndpointClient : IRewardEndpointClient
    {
        private readonly ILiveOpsService liveOpsService;

        public LiveOpsRewardEndpointClient(ILiveOpsService liveOpsService)
        {
            this.liveOpsService = liveOpsService;
        }

        public async Task<bool> CallRewardEndpointAsync(string unityUserId, string placementId, string rewardAdId)
        {
            Debug.Log($"[LiveOpsRewardEndpointClient] Sending WatchAdRequest for placement {placementId} with adId {rewardAdId}");

            var request = new WatchAdRequest
            {
                PlacementId = placementId
            };

            var response = await liveOpsService.CallAsync(request);

            if (response != null && response.IsSuccess())
            {
                Debug.Log($"[LiveOpsRewardEndpointClient] Successfully validated reward for placement {placementId}");
                return true;
            }

            Debug.LogError($"[LiveOpsRewardEndpointClient] Failed to validate reward for placement {placementId}");
            return false;
        }
    }
}

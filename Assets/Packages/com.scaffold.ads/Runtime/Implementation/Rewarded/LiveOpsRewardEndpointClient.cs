using System.Threading.Tasks;
using LiveOps.Modules.DTO.ModuleRequests;
using Scaffold.LiveOps;
using UnityEngine;

namespace Scaffold.Ads
{
    public class LiveOpsRewardEndpointClient : IRewardEndpointClient
    {
        public LiveOpsRewardEndpointClient(ILiveOpsService liveOpsService)
        {
            this.liveOpsService = liveOpsService;
        }

        private readonly ILiveOpsService liveOpsService;

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

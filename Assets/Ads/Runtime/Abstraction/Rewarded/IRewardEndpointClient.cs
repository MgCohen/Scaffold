using System.Threading.Tasks;

namespace Game.Ads.Endpoint
{
    public interface IRewardEndpointClient
    {
        Task<bool> CallRewardEndpointAsync(string unityUserId, string rewardAdId, string customEndpointUrl);
    }
}
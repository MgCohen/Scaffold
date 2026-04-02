using System.Threading.Tasks;

namespace Scaffold.Ads
{
    public interface IRewardEndpointClient
    {
        Task<bool> CallRewardEndpointAsync(string unityUserId, string placementId, string rewardAdId);
    }
}
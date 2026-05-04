using System;
using System.Threading.Tasks;

namespace Scaffold.Ads
{
    public interface IAdProvider : IDisposable
    {
        Task Initialize(string userId);
        void SetMuted(bool mute);
        string UserId { get; set; }
        IRewardedAdService RewardedAdService { get; }
        IInterstitialAdService InterstitialAdService { get; }
        IBannerAdService BannerAdService { get; }
    }
}

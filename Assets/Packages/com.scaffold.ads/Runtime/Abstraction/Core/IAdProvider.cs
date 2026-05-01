using System;
using UnityEngine;

namespace Scaffold.Ads
{
    public interface IAdProvider : IDisposable
    {
        Awaitable Initialize(string userId);
        void SetMuted(bool mute);
        string UserId { get; set; }
        IRewardedAdService RewardedAdService { get; }
        IInterstitialAdService InterstitialAdService { get; }
        IBannerAdService BannerAdService { get; }
    }
}

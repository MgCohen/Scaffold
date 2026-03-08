using System;
using UnityEngine;

namespace Game.Ads.Interfaces
{
    public interface IAdProvider : IDisposable
    {
        Awaitable Initialize();
        void SetMuted(bool mute);
        string UserId { get; set; }
        IRewardedAdService RewardedAdService { get; }
        IInterstitialAdService InterstitialAdService { get; }
        IBannerAdService BannerAdService { get; }
    }
}

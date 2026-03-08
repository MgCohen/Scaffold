using System;
using UnityEngine;

namespace Game.Ads.Interfaces
{
    public interface IInterstitialAdService : IDisposable
    {
        Awaitable<bool> CanShowAd(string placementName = null);
        void ShowAd(string placementName = null);

        event Action<bool> AdAvailable;
        event Action<bool, string> AdSuccessfullyCompleted; // success, placementName
    }
}

using System;
using UnityEngine;

namespace Scaffold.Ads
{
    public interface IRewardedAdService
    {
        Awaitable<bool> CanShowAd(string placementName = null);
        void ShowAd(string placementName = null);
        
        event Action<bool> AdAvailable;
        event Action<bool, string, string> AdSuccessfullyCompletedWithToken; // success, placementName, token
        event Action<bool, string> AdSuccessfullyCompleted; // success, placementName
    }
}

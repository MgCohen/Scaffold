using System;
using Game.Ads.Configurations;

namespace Game.Ads.Interfaces
{
    public interface IBannerAdService : IDisposable
    {
        void LoadBanner(string placementName = null);
        void ShowBanner(string placementName = null, BannerPosition? position = null);
        void HideBanner(string placementName = null);
        void DestroyBanner(string placementName = null);

        event Action<bool> BannerLoaded;
    }
}

using System;

namespace Scaffold.Ads
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

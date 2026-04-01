using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Ads.Test
{
    public class BannerAdTester : MonoBehaviour
    {
        [Header("Ad System")]
        public AdManager adManager;

        [Header("Banner Ads")]
        public AdPlacementKeySO bannerDefaultKey;
        public Button btnShowBannerDefault;
        public Button btnHideBannerDefault;

        private void Start()
        {
            if (btnShowBannerDefault != null) btnShowBannerDefault.onClick.AddListener(() => ShowBannerAd(bannerDefaultKey));
            if (btnHideBannerDefault != null) btnHideBannerDefault.onClick.AddListener(() => HideBannerAd(bannerDefaultKey));
        }

        private void OnDestroy()
        {
            if (btnShowBannerDefault != null) btnShowBannerDefault.onClick.RemoveAllListeners();
            if (btnHideBannerDefault != null) btnHideBannerDefault.onClick.RemoveAllListeners();
        }

        public void ShowBannerAd(string placement)
        {
            Debug.Log($"Test.ShowBannerAd [{placement}] via new GlobalAdManager");
            if (adManager != null && adManager.BannerAds != null)
            {
                adManager.BannerAds.ShowBanner(placement);
            }
        }

        public void HideBannerAd(string placement)
        {
            Debug.Log($"Test.HideBannerAd [{placement}] via new GlobalAdManager");
            if (adManager != null && adManager.BannerAds != null)
            {
                adManager.BannerAds.HideBanner(placement);
            }
        }
    }
}

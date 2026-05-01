using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Ads.Levelplay.Test
{
    public class BannerAdTester : MonoBehaviour
    {
        [Header("Ad System")]
        public AdManager AdManager;

        [Header("Banner Ads")]
        public AdPlacementKeySO BannerDefaultKey;
        public Button BtnShowBannerDefault;
        public Button BtnHideBannerDefault;

        private void Start()
        {
            if (BtnShowBannerDefault != null)
            {
                BtnShowBannerDefault.onClick.AddListener(() => ShowBannerAd(BannerDefaultKey));
            }
            if (BtnHideBannerDefault != null)
            {
                BtnHideBannerDefault.onClick.AddListener(() => HideBannerAd(BannerDefaultKey));
            }
        }

        private void OnDestroy()
        {
            if (BtnShowBannerDefault != null)
            {
                BtnShowBannerDefault.onClick.RemoveAllListeners();
            }
            if (BtnHideBannerDefault != null)
            {
                BtnHideBannerDefault.onClick.RemoveAllListeners();
            }
        }

        public void ShowBannerAd(string placement)
        {
            Debug.Log($"Test.ShowBannerAd [{placement}] via new GlobalAdManager");
            if (AdManager != null && AdManager.BannerAds != null)
            {
                AdManager.BannerAds.ShowBanner(placement);
            }
        }

        public void HideBannerAd(string placement)
        {
            Debug.Log($"Test.HideBannerAd [{placement}] via new GlobalAdManager");
            if (AdManager != null && AdManager.BannerAds != null)
            {
                AdManager.BannerAds.HideBanner(placement);
            }
        }
    }
}

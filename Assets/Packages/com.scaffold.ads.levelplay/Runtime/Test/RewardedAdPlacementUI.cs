using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Ads.Levelplay.Test
{
    [Serializable]
    public class RewardedAdPlacementUI
    {
        [HideInInspector]
        public bool IsFetching;
        [HideInInspector]
        public bool IsAdAvailable;

        public AdPlacementKeySO Key;
        public Button ButtonShow;
        public Button ButtonFetch;
        public TextMeshProUGUI StatusText;
        public TextMeshProUGUI CooldownText;
    }
}

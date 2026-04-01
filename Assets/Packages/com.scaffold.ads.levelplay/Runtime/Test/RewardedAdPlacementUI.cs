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
        public bool isFetching;
        [HideInInspector]
        public bool isAdAvailable;

        public AdPlacementKeySO key;
        public Button buttonShow;
        public Button buttonFetch;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI cooldownText;
    }
}
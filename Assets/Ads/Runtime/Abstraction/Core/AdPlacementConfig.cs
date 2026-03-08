using System;
using UnityEngine;

namespace Game.Ads.Configurations
{
    [Serializable]
    public abstract class AdPlacementConfig
    {
        [Tooltip("The actual placement string passed to the SDKs (e.g. 'Test_Placement')")]
        public string adUnitId;
        [Tooltip("The strongly-typed ScriptableObject reference used in code to identify this placement.")]
        public AdPlacementKeySO placementKey;
    }
}

using System;
using UnityEngine;

namespace Scaffold.Ads
{
    [Serializable]
    public abstract class AdPlacementConfig
    {
        [Tooltip("The actual placement string passed to the SDKs (e.g. 'Test_Placement')")]
        public string AdUnitId;
        [Tooltip("The strongly-typed ScriptableObject reference used in code to identify this placement.")]
        public AdPlacementKeySO PlacementKey;
    }
}

#if UNITY_6000_5_OR_NEWER
using System.Collections.Generic;
using Unity.Services.Analytics;
#endif
using UnityEngine;
using VContainer.Unity;
using UGSAnalyticsService = Unity.Services.Analytics.AnalyticsService;

namespace Scaffold.Analytics
{
    public sealed class AnalyticsService : IAnalyticsService, IInitializable
    {
        public void Initialize()
        {
#if !UNITY_EDITOR
            UGSAnalyticsService.Instance.StartDataCollection();
#endif
            Debug.Log("[AnalyticsService] Initialized and Data Collection Started.");
        }

        public void Record<T>(T evt) where T : AnalyticsEvent
        {
            Debug.Log($"[AnalyticsService] Sending event of type '{typeof(T).FullName}'.");

            if (UGSAnalyticsService.Instance != null)
            {
#if UNITY_6000_5_OR_NEWER
                CustomEvent customEvent = new CustomEvent(evt.Name);
                foreach (KeyValuePair<string, object> parameter in evt.Parameters)
                {
                    customEvent.Add(parameter.Key, parameter.Value);
                }

                UGSAnalyticsService.Instance.RecordEvent(customEvent);
#else
                UGSAnalyticsService.Instance.CustomData(evt.Name, evt.Parameters);
#endif
            }
        }
    }
}

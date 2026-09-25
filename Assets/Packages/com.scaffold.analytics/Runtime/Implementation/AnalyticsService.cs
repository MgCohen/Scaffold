using System;
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
            try
            {
                if (evt == null)
                {
                    throw new ArgumentNullException(nameof(evt));
                }

                Debug.Log($"[AnalyticsService] Sending event of type '{typeof(T).FullName}'.");
                if (UGSAnalyticsService.Instance == null)
                {
                    throw new InvalidOperationException("UGS Analytics is not initialized.");
                }

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
            catch (Exception exception)
            {
                Debug.LogError($"[AnalyticsService] Failed to record analytics event: {exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }

        public void Flush()
        {
            try
            {
                if (UGSAnalyticsService.Instance == null)
                {
                    throw new InvalidOperationException("UGS Analytics is not initialized.");
                }

                UGSAnalyticsService.Instance.Flush();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AnalyticsService] Failed to flush analytics events: {exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.AppFlow
{
    public sealed class AppFlowErrorHandler : IAppFlowErrorHandler
    {
        public const string ReportedDataKey = "AppFlow.Reported";

        private const int ringBufferSize = 64;

        public IReadOnlyList<AppFlowErrorInfo> Recent
        {
            get
            {
                var list = new List<AppFlowErrorInfo>(ringCount);
                for (int i = 0; i < ringCount; i++)
                {
                    list.Add(ring[(ringStart + i) % ringBufferSize]);
                }

                return list;
            }
        }

        private readonly AppFlowErrorInfo[] ring = new AppFlowErrorInfo[ringBufferSize];

        private int ringCount;

        private int ringStart;

        public event Action<AppFlowErrorInfo> OnError;

        public void Report(string source, Exception exception)
        {
            Report(new AppFlowErrorInfo(AppFlowErrorPhase.Manual, null, source, exception, DateTime.UtcNow));
        }

        public void Report(AppFlowErrorInfo info)
        {
            bool shouldLog = DetermineShouldLog(info);
            PushRing(info);
            if (shouldLog)
            {
                LogReport(info);
            }

            InvokeOnErrorSafe(info);
        }

        private bool DetermineShouldLog(AppFlowErrorInfo info)
        {
            if (info.Exception == null)
            {
                return true;
            }

            if (info.Exception.Data.Contains(ReportedDataKey))
            {
                return false;
            }

            info.Exception.Data[ReportedDataKey] = true;
            return true;
        }

        private void PushRing(AppFlowErrorInfo info)
        {
            int index = (ringStart + ringCount) % ringBufferSize;
            if (ringCount < ringBufferSize)
            {
                ring[index] = info;
                ringCount++;
            }
            else
            {
                ring[ringStart] = info;
                ringStart = (ringStart + 1) % ringBufferSize;
            }
        }

        private void LogReport(AppFlowErrorInfo info)
        {
            if (info.Exception != null)
            {
                string layer = string.IsNullOrEmpty(info.LayerName) ? string.Empty : $"layer '{info.LayerName}' ";
                Debug.LogError($"[AppFlow] {info.Phase} {layer}{info.Source}: {info.Exception.Message}\n{info.Exception.StackTrace}");
            }
            else
            {
                Debug.LogError($"[AppFlow] {info.Phase} {info.Source}");
            }
        }

        private void InvokeOnErrorSafe(AppFlowErrorInfo info)
        {
            var handler = OnError;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(info);
            }
            catch (Exception cbEx)
            {
                Debug.LogError($"[AppFlow] OnError subscriber threw: {cbEx.Message}\n{cbEx.StackTrace}");
            }
        }
    }
}

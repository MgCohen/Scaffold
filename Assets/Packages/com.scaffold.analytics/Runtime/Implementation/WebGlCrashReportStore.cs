using System;
using System.Runtime.InteropServices;

namespace Scaffold.Analytics
{
    internal sealed class WebGlCrashReportStore : IWebGlCrashReportStore
    {
        public bool IsAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public string ReadReport()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int byteCount = ScaffoldAnalyticsGetWebGlCrashReportLength();
            if (byteCount <= 0)
            {
                return string.Empty;
            }

            IntPtr buffer = Marshal.AllocHGlobal(byteCount + 1);
            try
            {
                ScaffoldAnalyticsCopyWebGlCrashReport(buffer, byteCount + 1);
                return Marshal.PtrToStringUTF8(buffer) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
#else
            return string.Empty;
#endif
        }

        public void Acknowledge(string sessionId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ScaffoldAnalyticsAcknowledgeWebGlCrashReport(sessionId);
#endif
        }

        public void SetCheckpoint(string phase, string details)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ScaffoldAnalyticsSetWebGlCheckpoint(phase, details ?? string.Empty);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int ScaffoldAnalyticsGetWebGlCrashReportLength();

        [DllImport("__Internal")]
        private static extern void ScaffoldAnalyticsCopyWebGlCrashReport(IntPtr buffer, int capacity);

        [DllImport("__Internal")]
        private static extern void ScaffoldAnalyticsAcknowledgeWebGlCrashReport(string sessionId);

        [DllImport("__Internal")]
        private static extern void ScaffoldAnalyticsSetWebGlCheckpoint(string phase, string details);
#endif
    }
}

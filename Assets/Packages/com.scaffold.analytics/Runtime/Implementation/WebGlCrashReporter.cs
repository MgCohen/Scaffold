using System;
using UnityEngine;

namespace Scaffold.Analytics
{
    public sealed class WebGlCrashReporter : IWebGlCrashReporter
    {
        private const int MaximumStringLength = 100;
        private const double BytesPerMegabyte = 1024d * 1024d;

        private readonly IAnalyticsService _analyticsService;
        private readonly IWebGlCrashReportStore _reportStore;
        private string _lastReportedSessionId;

        public WebGlCrashReporter(IAnalyticsService analyticsService)
            : this(analyticsService, new WebGlCrashReportStore())
        {
        }

        internal WebGlCrashReporter(
            IAnalyticsService analyticsService,
            IWebGlCrashReportStore reportStore)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _reportStore = reportStore ?? throw new ArgumentNullException(nameof(reportStore));
        }

        public bool TryReportPreviousSession()
        {
            try
            {
                if (!_reportStore.IsAvailable)
                {
                    return false;
                }

                string reportJson = _reportStore.ReadReport();
                if (string.IsNullOrWhiteSpace(reportJson))
                {
                    return false;
                }

                WebGlDiagnosticReport report = JsonUtility.FromJson<WebGlDiagnosticReport>(reportJson);
                WebGlDiagnosticSession previous = report?.previous;
                if (previous == null ||
                    !report.previousEndedAbruptly ||
                    previous.cleanExit ||
                    string.IsNullOrWhiteSpace(previous.sessionId) ||
                    previous.sessionId == _lastReportedSessionId)
                {
                    return false;
                }

                WebGlDiagnosticEvent lastError = FindLastError(previous.events);
                long durationMs = Math.Max(0L, previous.updatedAt - previous.startedAt);
                WebGlDiagnosticEnvironment environment = previous.environment ?? new WebGlDiagnosticEnvironment();
                WebGlAbruptSessionEvent analyticsEvent = new WebGlAbruptSessionEvent(
                    Truncate(previous.sessionId),
                    Truncate(previous.phase),
                    previous.runtimeReady,
                    durationMs,
                    Truncate(environment.userAgent),
                    Truncate(environment.platform),
                    Truncate(environment.viewport),
                    environment.devicePixelRatio,
                    Math.Max(0d, environment.wasmHeapBytes / BytesPerMegabyte),
                    Truncate(lastError?.phase ?? previous.phase),
                    Truncate(GetErrorMessage(lastError?.details)));

                _analyticsService.Record(analyticsEvent);
                _analyticsService.Flush();
                _reportStore.Acknowledge(previous.sessionId);
                _lastReportedSessionId = previous.sessionId;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WebGlCrashReporter] Failed to recover the previous WebGL session: {exception.Message}\n{exception.StackTrace}");
                return false;
            }
        }

        public void SetCheckpoint(string phase, string details = null)
        {
            try
            {
                if (!_reportStore.IsAvailable)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(phase))
                {
                    throw new ArgumentException("Checkpoint phase cannot be null or whitespace.", nameof(phase));
                }

                _reportStore.SetCheckpoint(Truncate(phase), Truncate(details));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WebGlCrashReporter] Failed to persist checkpoint: {exception.Message}\n{exception.StackTrace}");
            }
        }

        private static WebGlDiagnosticEvent FindLastError(WebGlDiagnosticEvent[] events)
        {
            if (events == null || events.Length == 0)
            {
                return null;
            }

            for (int index = events.Length - 1; index >= 0; index--)
            {
                string phase = events[index]?.phase;
                if (ContainsErrorKeyword(phase))
                {
                    return events[index];
                }
            }

            return events[events.Length - 1];
        }

        private static bool ContainsErrorKeyword(string phase)
        {
            if (string.IsNullOrEmpty(phase))
            {
                return false;
            }

            return phase.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                phase.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                phase.IndexOf("lost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                phase.IndexOf("rejection", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetErrorMessage(WebGlDiagnosticDetails details)
        {
            if (details == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(details.message))
            {
                return details.message;
            }

            if (!string.IsNullOrEmpty(details.statusMessage))
            {
                return details.statusMessage;
            }

            return details.loaderUrl ?? string.Empty;
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaximumStringLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, MaximumStringLength);
        }

        [Serializable]
        private sealed class WebGlDiagnosticReport
        {
            public bool previousEndedAbruptly;
            public WebGlDiagnosticSession previous;
        }

        [Serializable]
        private sealed class WebGlDiagnosticSession
        {
            public string sessionId;
            public long startedAt;
            public long updatedAt;
            public string phase;
            public bool cleanExit;
            public bool runtimeReady;
            public WebGlDiagnosticEvent[] events;
            public WebGlDiagnosticEnvironment environment;
        }

        [Serializable]
        private sealed class WebGlDiagnosticEvent
        {
            public string phase;
            public WebGlDiagnosticDetails details;
        }

        [Serializable]
        private sealed class WebGlDiagnosticDetails
        {
            public string message;
            public string statusMessage;
            public string loaderUrl;
        }

        [Serializable]
        private sealed class WebGlDiagnosticEnvironment
        {
            public string userAgent;
            public string platform;
            public string viewport;
            public double devicePixelRatio;
            public double wasmHeapBytes;
        }
    }
}

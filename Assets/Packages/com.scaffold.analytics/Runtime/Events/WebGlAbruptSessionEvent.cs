namespace Scaffold.Analytics
{
    /// <summary>
    /// Analytics event emitted when the previous WebGL session did not exit cleanly.
    /// </summary>
    public sealed class WebGlAbruptSessionEvent : AnalyticsEvent
    {
        public const string EventName = "webglAbruptSession";

        public WebGlAbruptSessionEvent(
            string sessionId,
            string lastPhase,
            bool runtimeReady,
            long durationMs,
            string userAgent,
            string browserPlatform,
            string viewport,
            double devicePixelRatio,
            double wasmHeapMb,
            string lastErrorType,
            string lastErrorMessage)
            : base(EventName)
        {
            SetParameter("sessionId", sessionId);
            SetParameter("lastPhase", lastPhase);
            SetParameter("runtimeReady", runtimeReady);
            SetParameter("durationMs", durationMs);
            SetParameter("userAgent", userAgent);
            SetParameter("browserPlatform", browserPlatform);
            SetParameter("viewport", viewport);
            SetParameter("devicePixelRatio", devicePixelRatio);
            SetParameter("wasmHeapMb", wasmHeapMb);
            SetParameter("lastErrorType", lastErrorType);
            SetParameter("lastErrorMessage", lastErrorMessage);
        }
    }
}

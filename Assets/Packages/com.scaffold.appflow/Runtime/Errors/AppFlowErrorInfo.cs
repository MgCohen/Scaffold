using System;

namespace Scaffold.AppFlow
{
    public readonly struct AppFlowErrorInfo
    {
        public AppFlowErrorInfo(AppFlowErrorPhase phase, string layerName, string source, Exception exception, DateTime timestampUtc)
        {
            Phase = phase;
            LayerName = layerName;
            Source = source ?? string.Empty;
            Exception = exception;
            TimestampUtc = timestampUtc;
        }

        public AppFlowErrorPhase Phase { get; }

        public string LayerName { get; }

        public string Source { get; }

        public Exception Exception { get; }

        public DateTime TimestampUtc { get; }
    }
}

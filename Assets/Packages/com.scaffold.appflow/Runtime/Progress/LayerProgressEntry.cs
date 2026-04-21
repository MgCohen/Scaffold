namespace Scaffold.AppFlow
{
    public readonly struct LayerProgressEntry
    {
        public LayerProgressEntry(string layerName, LayerStatus status, float subProgress, AppFlowErrorInfo? lastError)
        {
            LayerName = layerName ?? string.Empty;
            Status = status;
            SubProgress = subProgress;
            LastError = lastError;
        }

        public string LayerName { get; }

        public LayerStatus Status { get; }

        public float SubProgress { get; }

        public AppFlowErrorInfo? LastError { get; }
    }
}

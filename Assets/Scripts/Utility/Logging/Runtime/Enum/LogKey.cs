namespace Scaffold.Logging
{
    /// <summary>
    /// Represents predefined logging tags or categories to classify messages.
    /// The main goal is to enforce standardized keywords inside log messages for easier parsing and filtering.
    /// It is used when building contextual information directly into telemetry output.
    /// </summary>
    public enum LogKey
    {
        Starting,
        Initialized,
        Success,
        Canceled,
        Faulted,
        Exception,
        Signal,
        Assert,
        Player,
        Network,
        Match
    }
}
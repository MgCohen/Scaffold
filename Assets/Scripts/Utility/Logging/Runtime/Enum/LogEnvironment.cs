namespace Scaffold.Logging
{
    /// <summary>
    /// Specifies the target platform or environment where a log is generated.
    /// The main goal is to distinguish logs originating natively on the client vs server.
    /// It is used by the GameDebug system to accurately tag the origin of shared log calls.
    /// </summary>
    public enum LogEnvironment
    {
        Client,
        Server,
        Shared
    }
}
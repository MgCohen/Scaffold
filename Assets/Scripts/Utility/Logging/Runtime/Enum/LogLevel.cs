namespace Scaffold.Logging
{
    /// <summary>
    /// Defines the severity levels applicable to debugging messages.
    /// The main goal is to segment the priority of diagnostic outputs.
    /// It is used natively by the ILogger integration to map against Unity's console or remote dashboards.
    /// </summary>
    public enum LogLevel
    {
        Log,
        Warning,
        Error
    }
}
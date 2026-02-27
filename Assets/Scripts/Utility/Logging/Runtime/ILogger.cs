namespace Scaffold.Logging
{
    /// <summary>
    /// Declares the contract for emitting diagnostic strings across varying environments.
    /// The main goal is to decouple the GameDebug facade from explicit endpoints like Unity Console.
    /// It is used strictly internally by the framework's logging subsystem to route traffic flexibly.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Emits a formatted log message.
        /// The main goal is to route the text output efficiently to the target handler.
        /// It is used by the GameDebug system whenever a valid string is produced.
        /// </summary>
        void Log(LogLevel level, string message);
    }
}
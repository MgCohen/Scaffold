namespace Scaffold.Logging
{
    /// <summary>
    /// Declares the contract for emitting diagnostic strings across varying environments.
    /// The main goal is to decouple the GameDebug facade from explicit endpoints like Unity Console.
    /// It is used strictly internally by the framework's logging subsystem to route traffic flexibly.
    /// </summary>
    public interface ILogger
    {
        void Log(LogLevel level, string message);
    }
}
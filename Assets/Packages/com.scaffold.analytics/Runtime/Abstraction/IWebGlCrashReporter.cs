namespace Scaffold.Analytics
{
    /// <summary>
    /// Recovers diagnostic checkpoints from the previous WebGL browser session.
    /// </summary>
    public interface IWebGlCrashReporter
    {
        /// <summary>
        /// Records an abrupt previous browser session and acknowledges it after Analytics flushes.
        /// </summary>
        /// <returns>True when a previous session was recorded.</returns>
        bool TryReportPreviousSession();

        /// <summary>
        /// Persists the latest runtime checkpoint in browser storage.
        /// </summary>
        /// <param name="phase">A stable phase identifier.</param>
        /// <param name="details">Optional bounded diagnostic details.</param>
        void SetCheckpoint(string phase, string details = null);
    }
}

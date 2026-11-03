namespace Scaffold.Analytics
{
    internal interface IWebGlCrashReportStore
    {
        bool IsAvailable { get; }

        string ReadReport();

        void Acknowledge(string sessionId);

        void SetCheckpoint(string phase, string details);
    }
}

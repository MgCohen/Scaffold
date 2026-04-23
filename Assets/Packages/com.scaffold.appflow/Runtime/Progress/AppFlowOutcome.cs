using System;

namespace Scaffold.AppFlow
{
    public readonly struct AppFlowOutcome
    {
        private AppFlowOutcome(bool succeeded, Exception error, bool cancelled)
        {
            Succeeded = succeeded;
            Error = error;
            Cancelled = cancelled;
        }

        public bool Succeeded { get; }

        public Exception Error { get; }

        public bool Cancelled { get; }

        public static AppFlowOutcome CreateSuccess()
        {
            return new AppFlowOutcome(true, null, false);
        }

        public static AppFlowOutcome CreateFailed(Exception error)
        {
            return new AppFlowOutcome(false, error, false);
        }

        public static AppFlowOutcome CreateCancelled()
        {
            return new AppFlowOutcome(false, null, true);
        }
    }
}

namespace Scaffold.Analytics
{
    public class ErrorLoggedEvent : AnalyticsEvent
    {
        public ErrorLoggedEvent(string errorMessage, string errorContext) : base("errorLogged")
        {
            SetParameter("ErrorMessage", errorMessage);
            SetParameter("ErrorContext", errorContext);
        }
    }
}

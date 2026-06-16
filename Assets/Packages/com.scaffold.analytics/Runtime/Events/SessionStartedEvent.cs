namespace Scaffold.Analytics
{
    public class SessionStartedEvent : AnalyticsEvent
    {
        public SessionStartedEvent(int startHour) : base("sessionStarted")
        {
            SetParameter("StartHour", startHour);
        }
    }
}

namespace Scaffold.Analytics
{
    public class TutorialStartedEvent : AnalyticsEvent
    {
        public TutorialStartedEvent(string tutorialId) : base("tutorialStarted") 
        {
            SetParameter("TutorialID", tutorialId);
        }
    }
}

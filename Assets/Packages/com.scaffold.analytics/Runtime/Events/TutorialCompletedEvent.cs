namespace Scaffold.Analytics
{
    public class TutorialCompletedEvent : AnalyticsEvent
    {
        public TutorialCompletedEvent(string tutorialId) : base("tutorialFinished") 
        {
            SetParameter("TutorialID", tutorialId);
        }
    }
}

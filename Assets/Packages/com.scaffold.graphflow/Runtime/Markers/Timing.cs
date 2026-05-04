namespace Scaffold.GraphFlow
{
    /// <summary>Trigger phase relative to the event's surrounding action (post-M3 decision #3).
    /// Used by <c>OnTrigger&lt;TEvent&gt;</c> to discriminate Before/After subscriptions on the host's
    /// event bus.</summary>
    public enum Timing
    {
        Before,
        After,
    }
}

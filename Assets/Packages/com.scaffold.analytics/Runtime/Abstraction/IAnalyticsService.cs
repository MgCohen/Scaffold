namespace Scaffold.Analytics
{
    /// <summary>
    /// Service for recording strongly-typed analytics events.
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Records an analytics event.
        /// </summary>
        /// <typeparam name="T">The type of the event.</typeparam>
        /// <param name="evt">The event instance to record.</param>
        void Record<T>(T evt) where T : AnalyticsEvent;
    }
}

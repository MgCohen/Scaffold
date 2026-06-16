
namespace Scaffold.Analytics
{
    /// <summary>
    /// Base class for all strong-typed analytics events.
    /// Inherits from UGS Event to allow parameter setting.
    /// </summary>
    public abstract class AnalyticsEvent
    {
        public string Name { get; }
        public System.Collections.Generic.Dictionary<string, object> Parameters { get; }

        protected AnalyticsEvent(string name)
        {
            Name = name;
            Parameters = new System.Collections.Generic.Dictionary<string, object>();
        }

        protected void SetParameter(string name, object value)
        {
            Parameters[name] = value;
        }
    }
}

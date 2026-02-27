using UnityEngine;

namespace Scaffold.Logging
{
    /// <summary>
    /// Implements standard Unity Console integration for the agnostic debug interface.
    /// The main goal is to map custom internal levels appropriately into UnityEngine.Debug methodologies.
    /// It is used as the default sink out-of-the-box by the shared logging facade.
    /// </summary>
    public class UnityLogger : ILogger
    {
        public void Log(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;

                case LogLevel.Error:
                    Debug.LogError(message);
                    break;

                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}
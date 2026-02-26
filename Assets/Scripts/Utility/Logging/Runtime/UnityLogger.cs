using UnityEngine;

namespace Scaffold.Logging
{
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
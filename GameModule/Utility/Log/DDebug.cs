using Microsoft.Extensions.Logging;

namespace GameModuleDTO.Initialize
{
    public static class DDebug
    {
        public static ILogger ILogger;
        
        public static void Log(string message)
        {
            if (ILogger == null)
            {
                return;
            }
            ILogger.LogInformation(message);
        }
        
        public static void LogWarning(string message)
        {
            if (ILogger == null)
            {
                return;
            }
            ILogger.LogWarning(message);
        }
        
        public static void LogError(string message)
        {
            if (ILogger == null)
            {
                return;
            }
            ILogger.LogError(message);
        }
    }
}
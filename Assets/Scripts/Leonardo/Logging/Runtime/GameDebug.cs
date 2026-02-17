using System;
using System.Linq;

namespace Scaffold.Logging
{
    /// <summary>
    /// Central logging facade used across Client, Server and Shared code.
    /// Handles environment tagging, log levels and key/value-style context.
    /// </summary>
    public static class GameDebug
    {
        /// <summary>
        /// Concrete logger implementation (Unity, Console, File, etc).
        /// This can be replaced without changing the public API.
        /// </summary>
        private static ILogger LoggerImpl = new UnityLogger();
        
        /// <summary>
        /// Indicates whether the current runtime is Server or Client.
        /// Used by Shared logging methods.
        /// </summary>
        public static bool IsServer { get; set; }

        /// <summary>
        /// Resolves the current environment based on IsServer.
        /// Used by Shared log methods.
        /// </summary>
        private static LogEnvironment currentEnvironment;

        /// <summary>
        /// Call upon initialization to set the environment and optional custom logger.
        /// </summary>
        public static void Initialize(bool isServer, ILogger newLogger = null)
        {
            IsServer = isServer;
            currentEnvironment = IsServer ? LogEnvironment.Server : LogEnvironment.Client;
            if (newLogger != null)
            {
                LoggerImpl = newLogger;
            }
        }

        #region Client

        public static string LogClient(object message, params object[] keys)
        {
            return Log(LogEnvironment.Client, LogLevel.Log, message, keys);
        }

        public static string LogClientWarning(object message, params object[] keys)
        {
            return Log(LogEnvironment.Client, LogLevel.Warning, message, keys);
        }

        public static string LogClientError(object message, params object[] keys)
        {
            return Log(LogEnvironment.Client, LogLevel.Error, message, keys);
        }

        public static string LogClientStarting(params object[] keys)
        {
            return LogWithImplicitKey(LogEnvironment.Client, LogLevel.Log, LogKey.Starting, keys);
        }

        public static string LogClientInitialized(params object[] keys)
        {
            return LogWithImplicitKey(LogEnvironment.Client, LogLevel.Log, LogKey.Initialized, keys);
        }

        #endregion

        #region Server

        public static string LogServer(object message, params object[] keys)
        {
            return Log(LogEnvironment.Server, LogLevel.Log, message, keys);
        }

        public static string LogServerWarning(object message, params object[] keys)
        {
            return Log(LogEnvironment.Server, LogLevel.Warning, message, keys);
        }

        public static string LogServerError(object message, params object[] keys)
        {
            return Log(LogEnvironment.Server, LogLevel.Error, message, keys);
        }

        public static string LogServerException(Exception exception, params object[] keys)
        {
            return LogException(LogEnvironment.Server, exception, keys);
        }

        #endregion

        #region Shared

        /// <summary>
        /// Logs a message using the current environment (Client or Server).
        /// </summary>
        public static string Log(object message, params object[] keys)
        {
            return Log(currentEnvironment, LogLevel.Log, message, keys);
        }

        /// <summary>
        /// Logs a warning using the current environment.
        /// </summary>
        public static string LogWarning(object message, params object[] keys)
        {
            return Log(currentEnvironment, LogLevel.Warning, message, keys);
        }

        /// <summary>
        /// Logs an error using the current environment.
        /// </summary>
        public static string LogError(object message, params object[] keys)
        {
            return Log(currentEnvironment, LogLevel.Error, message, keys);
        }

        /// <summary>
        /// Logs a shared "starting" lifecycle event.
        /// </summary>
        public static string LogStarting(params object[] keys)
        {
            return LogWithImplicitKey(currentEnvironment, LogLevel.Log, LogKey.Starting, keys);
        }

        /// <summary>
        /// Logs a shared "initialized" lifecycle event.
        /// </summary>
        public static string LogInitialized(params object[] keys)
        {
            return LogWithImplicitKey(currentEnvironment, LogLevel.Log, LogKey.Initialized, keys);
        }

        /// <summary>
        /// Logs an exception using the current environment context.
        /// </summary>
        public static string LogException(Exception exception, params object[] keys)
        {
            return LogException(currentEnvironment, exception, keys);
        }

        #endregion

        #region Core

        private static string Log(LogEnvironment environment, LogLevel level, object message, object[] keys)
        {
            string formattedMessage = FormatMessage(environment, level, message, keys);
            LoggerImpl.Log(level, formattedMessage);
            return formattedMessage;
        }

        private static string LogWithImplicitKey(LogEnvironment environment, LogLevel level, object implicitKey, object[] keys)
        {
            object[] combinedKeys = new[] { implicitKey }
                .Concat(keys ?? Array.Empty<object>())
                .ToArray();

            return Log(environment, level, implicitKey.ToString(), combinedKeys);
        }

        private static string LogException(LogEnvironment environment, Exception exception, object[] keys)
        {
             object[] combinedKeys = (keys ?? Array.Empty<object>())
                .Append(LogKey.Exception)
                .ToArray();

            return Log(environment, LogLevel.Error, exception, combinedKeys);
        }

        private static string FormatMessage(LogEnvironment environment, LogLevel level, object message, object[] keys)
        {
            string text = FormatValue(message);
            string formattedKeys = FormatKeys(keys);
            return $"[{environment}][{level}]{formattedKeys} {text}";
        }

        private static string FormatKeys(object[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return string.Empty;
            }
            string joined = string.Join("][", keys.Select(FormatValue));
            return "[" + joined + "]";
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is Exception exception)
            {
                return exception.ToString();
            }
            return value.ToString();
        }
        
        #endregion

        #region Assert

        /// <summary>
        /// Asserts that a condition is true. Throws an exception if false.
        /// </summary>
        public static void AssertThat(bool condition, string message = "Assertion failed", params object[] keys)
        {
            if (!condition)
            {
                AssertFail(message, keys);
            }
        }

        private static void AssertFail(string message, params object[] keys)
        {
            object[] combinedKeys = (keys ?? Array.Empty<object>())
                .Append(LogKey.Assert)
                .ToArray();

            LogError(message, combinedKeys);
            throw new ApplicationException(message);
        }

        #endregion
    }
}
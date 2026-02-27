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
        private static ILogger _loggerImpl = new UnityLogger();

        /// <summary>
        /// Indicates whether the current runtime is Server or Client.
        /// Used by Shared logging methods.
        /// </summary>
        public static bool IsServer { get; set; }

        /// <summary>
        /// Resolves the current environment based on IsServer.
        /// Used by Shared log methods.
        /// </summary>
        private static LogEnvironment _currentEnvironment;

        /// <summary>
        /// Call upon initialization to set the environment and optional custom logger.
        /// The main goal is to bootstrap the global debug proxy.
        /// It is used by the application lifecycle entry point.
        /// </summary>
        public static void Initialize(bool isServer, ILogger newLogger = null)
        {
            IsServer = isServer;
            _currentEnvironment = IsServer ? LogEnvironment.Server : LogEnvironment.Client;
            if (newLogger != null)
            {
                _loggerImpl = newLogger;
            }
        }

        #region Client

        /// <summary>
        /// Dispatches a standardized information message for the Client.
        /// The main goal is to emit telemetry explicitly labeled as client-side.
        /// It is used across frontend routines.
        /// </summary>
        public static string LogClient(object message, params object[] keys)
        {
            return Log(LogEnvironment.Client, LogLevel.Log, message, keys);
        }

        /// <summary>
        /// Dispatches a standardized warning message for the Client.
        /// The main goal is to call attention to non-fatal issues.
        /// It is used organically inside client modules.
        /// </summary>
        public static string LogClientWarning(object message, params object[] keys)
        {
            return Log(LogEnvironment.Client, LogLevel.Warning, message, keys);
        }

        /// <summary>
        /// Dispatches a standardized error message for the Client.
        /// The main goal is to broadcast critical application faults.
        /// It is used upon catching failing frontend operations.
        /// </summary>
        public static string LogClientError(object message, params object[] keys)
        {
            return Log(LogEnvironment.Client, LogLevel.Error, message, keys);
        }

        /// <summary>
        /// Automatically records the initialization start cycle on the Client.
        /// The main goal is to structure booting analytics predictably.
        /// It is used specifically at the beginning of service setups.
        /// </summary>
        public static string LogClientStarting(params object[] keys)
        {
            return LogWithImplicitKey(LogEnvironment.Client, LogLevel.Log, LogKey.Starting, keys);
        }

        /// <summary>
        /// Automatically records the initialization complete cycle on the Client.
        /// The main goal is to verify booting success.
        /// It is used conclusively at the end of service setups.
        /// </summary>
        public static string LogClientInitialized(params object[] keys)
        {
            return LogWithImplicitKey(LogEnvironment.Client, LogLevel.Log, LogKey.Initialized, keys);
        }

        #endregion

        #region Server

        /// <summary>
        /// Dispatches a standardized information message for the Server.
        /// The main goal is to emit telemetry explicitly labeled as backend-side.
        /// It is used across authoritative routines.
        /// </summary>
        public static string LogServer(object message, params object[] keys)
        {
            return Log(LogEnvironment.Server, LogLevel.Log, message, keys);
        }

        /// <summary>
        /// Dispatches a standardized warning message for the Server.
        /// The main goal is to call attention to non-fatal server issues.
        /// It is used securely observing backend states.
        /// </summary>
        public static string LogServerWarning(object message, params object[] keys)
        {
            return Log(LogEnvironment.Server, LogLevel.Warning, message, keys);
        }

        /// <summary>
        /// Dispatches a standardized error message for the Server.
        /// The main goal is to broadcast critical backend faults.
        /// It is used inherently reporting node panics.
        /// </summary>
        public static string LogServerError(object message, params object[] keys)
        {
            return Log(LogEnvironment.Server, LogLevel.Error, message, keys);
        }

        /// <summary>
        /// Structures and formats severe thrown exceptions dynamically.
        /// The main goal is to safely dump stack traces into cloud endpoints.
        /// It is used constantly in external try-catches.
        /// </summary>
        public static string LogServerException(Exception exception, params object[] keys)
        {
            return LogException(LogEnvironment.Server, exception, keys);
        }

        #endregion

        #region Shared

        /// <summary>
        /// Logs a message using the current environment (Client or Server).
        /// The main goal is to bridge generic shared logic uniformly.
        /// It is used by cross-compiled network scripts.
        /// </summary>
        public static string Log(object message, params object[] keys)
        {
            return Log(_currentEnvironment, LogLevel.Log, message, keys);
        }

        /// <summary>
        /// Logs a warning using the current environment.
        /// The main goal is to highlight agnostic misconfigurations.
        /// It is used consistently.
        /// </summary>
        public static string LogWarning(object message, params object[] keys)
        {
            return Log(_currentEnvironment, LogLevel.Warning, message, keys);
        }

        /// <summary>
        /// Logs an error using the current environment.
        /// The main goal is to halt gracefully on mutual faults.
        /// It is used prominently alongside asserting bounds.
        /// </summary>
        public static string LogError(object message, params object[] keys)
        {
            return Log(_currentEnvironment, LogLevel.Error, message, keys);
        }

        /// <summary>
        /// Logs a shared "starting" lifecycle event.
        /// The main goal is to track setup milestones remotely.
        /// It is used uniformly inside initializers.
        /// </summary>
        public static string LogStarting(params object[] keys)
        {
            return LogWithImplicitKey(_currentEnvironment, LogLevel.Log, LogKey.Starting, keys);
        }

        /// <summary>
        /// Logs a shared "initialized" lifecycle event.
        /// The main goal is to evaluate pipeline completeness natively.
        /// It is used predictably upon system readiness.
        /// </summary>
        public static string LogInitialized(params object[] keys)
        {
            return LogWithImplicitKey(_currentEnvironment, LogLevel.Log, LogKey.Initialized, keys);
        }

        /// <summary>
        /// Logs an exception using the current environment context.
        /// The main goal is to broadcast typed unhandled breaks locally.
        /// It is used dynamically.
        /// </summary>
        public static string LogException(Exception exception, params object[] keys)
        {
            return LogException(_currentEnvironment, exception, keys);
        }

        #endregion

        #region Core

        /// <summary>
        /// Orchestrates the lowest level string formatting and interface sink delegation.
        /// The main goal is to unify object arrays into the final console output.
        /// It is used by every internal public facade method transparently.
        /// </summary>
        private static string Log(LogEnvironment environment, LogLevel level, object message, object[] keys)
        {
            string formattedMessage = FormatMessage(environment, level, message, keys);
            _loggerImpl.Log(level, formattedMessage);
            return formattedMessage;
        }

        /// <summary>
        /// Injects an implicit tag key cleanly into the generic log stream.
        /// The main goal is to enforce baseline metadata without expanding parameters infinitely.
        /// It is used heavily by the discrete Signal and Lifecycle wrappers.
        /// </summary>
        private static string LogWithImplicitKey(LogEnvironment environment, LogLevel level, object implicitKey, object[] keys)
        {
            object[] combinedKeys = new[] { implicitKey }
                .Concat(keys ?? Array.Empty<object>())
                .ToArray();

            return Log(environment, level, implicitKey.ToString(), combinedKeys);
        }

        /// <summary>
        /// Extracts and formats crash logs explicitly routing them into the error stream.
        /// The main goal is to maintain visibility on stack trace depth locally.
        /// It is used fundamentally when catching backend logic limits.
        /// </summary>
        private static string LogException(LogEnvironment environment, Exception exception, object[] keys)
        {
            object[] combinedKeys = (keys ?? Array.Empty<object>())
               .Append(LogKey.Exception)
               .ToArray();

            return Log(environment, LogLevel.Error, exception, combinedKeys);
        }

        /// <summary>
        /// Compiles the final bracketed template joining environments, logs and arrays.
        /// The main goal is to yield a consistent read-format for external parsing software.
        /// It is used directly prior to interface delegation.
        /// </summary>
        private static string FormatMessage(LogEnvironment environment, LogLevel level, object message, object[] keys)
        {
            string text = FormatValue(message);
            string formattedKeys = FormatKeys(keys);
            return $"[{environment}][{level}]{formattedKeys} {text}";
        }

        /// <summary>
        /// Squashes variadic parameters into bracketed header identifiers.
        /// The main goal is to maintain structured tag hierarchy parsing.
        /// It is used primarily resolving keys.
        /// </summary>
        private static string FormatKeys(object[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return string.Empty;
            }
            string joined = string.Join("][", keys.Select(FormatValue));
            return "[" + joined + "]";
        }

        /// <summary>
        /// Converts generic structures into safe unrolled strings without looping refs implicitly.
        /// The main goal is to prevent recursive crashes when iterating raw objects.
        /// It is used broadly by formatting chains.
        /// </summary>
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
        /// The main goal is to ensure hard runtime halts on logic bugs instead of propagating bad data.
        /// It is used during development safely enforcing assumptions.
        /// </summary>
        public static void AssertThat(bool condition, string message = "Assertion failed", params object[] keys)
        {
            if (!condition)
            {
                AssertFail(message, keys);
            }
        }

        /// <summary>
        /// Terminates the application state throwing deeply formatted diagnostic strings.
        /// The main goal is to halt gracefully rendering context transparently.
        /// It is used by failing assertions locally.
        /// </summary>
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
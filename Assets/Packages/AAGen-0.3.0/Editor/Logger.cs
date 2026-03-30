using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents a way to filter messages to the console.
    /// </summary>
    public class Logger
    {
        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="Logger"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public Logger(DataContainer dataContainer)
        {
            // Cache the reference to the data container.
            m_DataContainer = dataContainer;
        }

        /// <summary>
        /// Logs a message to the console.
        /// </summary>
        /// <param name="invoker">The object that invoked the trace.</param>
        /// <param name="logLevel">The state indicating the level of detail for logging.</param>
        /// <param name="message">The message to log.</param>
        public void Log(object invoker, LogLevelID logLevel, string message)
        {
            // If there is a valid settings object assigned in the data container, then AAGen settings are created.
            // If AAGen settings are not yet created, then:
            if (m_DataContainer.Settings == null)
            {
                // Log an error.
                Debug.LogError($"Log failed! Settings = null!");

                // Do nothing else.
                return;
            }

            // Otherwise, the AAGen settings are not yet created.

            // If the logger should log an error, then:
            if (logLevel == LogLevelID.OnlyErrors)
            {
                // Log the message as an error.
                Debug.LogError($"{invoker.GetType().Name}: {message}");

                // Do nothing else.
                return;
            }

            // Otherwise, the logger should not log an error.

            // If the logger matches the log level of the AAGen settings, then:
            if (logLevel <= m_DataContainer.Settings.LogLevel)
            {
                // Log the message.
                Debug.Log($"{invoker.GetType().Name}: {message}");
            }
        }

        /// <summary>
        /// Logs an unexpected error message to the console.
        /// </summary>
        /// <param name="invoker">The object that invoked the trace.</param>
        /// <param name="message">The message to log.</param>
        public void LogError(object invoker, string message)
        {
            Log(invoker, LogLevelID.OnlyErrors, message);
        }

        /// <summary>
        /// Logs a detailed informational message to the console.
        /// </summary>
        /// <param name="invoker">The object that invoked the trace.</param>
        /// <param name="message">The message to log.</param>
        public void LogInfo(object invoker, string message)
        {
            Log(invoker, LogLevelID.Info, message);
        }

        /// <summary>
        /// Logs an extremely detailed message to the console.
        /// </summary>
        /// <param name="invoker">The object that invoked the trace.</param>
        /// <param name="message">The message to log.</param>
        public void LogDev(object invoker, string message)
        {
            Log(invoker, LogLevelID.Developer, message);
        }
        #endregion
    }
}

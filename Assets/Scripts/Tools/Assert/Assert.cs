namespace Scaffold.Tools.Assert
{
    using System;

    /// <summary>
    /// Provides assertion utilities for validating conditions and values.
    /// The main goal is to enforce invariants and throw standard exceptions on failure.
    /// </summary>
    /// <remarks>
    /// Used throughout the codebase to validate method arguments and state before proceeding with operations.
    /// </remarks>
    public static class Assert
    {
        /// <summary>
        /// Asserts that a given reference type value is not null.
        /// The main goal is to throw an ArgumentNullException if the value is null.
        /// </summary>
        /// <typeparam name="T">The type of the value being checked.</typeparam>
        /// <param name="value">The value to check for null.</param>
        /// <returns>True if the value is not null.</returns>
        /// <remarks>
        /// Used at the beginning of methods to validate required parameters.
        /// </remarks>
        public static bool IsNotNull<T>(T value) where T : class
        {
            if (value == null)
            {
                string paramName = nameof(value);
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null.");
            }
            return true;
        }

        /// <summary>
        /// Asserts that a given string is not null or empty.
        /// The main goal is to throw an ArgumentException if the string is empty or whitespace.
        /// </summary>
        /// <param name="value">The string value to check.</param>
        /// <param name="paramName">The name of the parameter for the exception message.</param>
        /// <returns>True if the string is valid.</returns>
        /// <remarks>
        /// Used to validate string inputs that must contain meaningful text.
        /// </remarks>
        public static bool IsNotEmpty(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{paramName} cannot be empty.", paramName);
            }
            return true;
        }

        /// <summary>
        /// Asserts that a given boolean condition is true.
        /// The main goal is to throw an InvalidOperationException if the condition is false.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="message">The exception message to use on failure.</param>
        /// <returns>True if the condition is true.</returns>
        /// <remarks>
        /// Used to enforce application state invariants or logic checks that must succeed.
        /// </remarks>
        public static bool IsTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
            return true;
        }
    }
}

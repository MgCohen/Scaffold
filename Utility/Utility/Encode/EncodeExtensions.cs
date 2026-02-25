using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Utility.Encode
{
    /// <summary>
    /// Provides extension methods and utilities for data encoding and validation.
    /// The main goal is to offer standardized functions for sanitizing keys and validating formats.
    /// </summary>
    /// <remarks>
    /// Used across the system when storing data with strict key character constraints or validating user input.
    /// </remarks>
    public static class EncodeExtensions
    {
        /// <summary>
        /// Checks if a string is a valid email format.
        /// The main goal is to ensure the string matches basic email structure.
        /// </summary>
        /// <param name="email">The email string to validate.</param>
        /// <returns>True if the email format is valid, otherwise false.</returns>
        /// <remarks>
        /// Used during user registration or input validation to prevent malformed email addresses.
        /// </remarks>
        public static bool IsValidEmail(string email)
        {
            // Simple regex pattern for email validation
            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return !string.IsNullOrWhiteSpace(email) && Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Sanitizes a string key to ensure it only contains allowed characters.
        /// The main goal is to safely encode strings that contain special characters into a URL-safe format.
        /// </summary>
        /// <param name="input">The original key string.</param>
        /// <returns>A sanitized, safe string key.</returns>
        /// <remarks>
        /// Used before saving data to databases or caches that restrict key characters (e.g. allowing only a-z, A-Z, 0-9, _, -).
        /// </remarks>
        public static string SanitizeKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Key input cannot be null or empty", nameof(input));
            }

            // Allowed characters: a-z, A-Z, 0-9, underscore (_), and hyphen (-)
            string allowedPattern = @"^[a-zA-Z0-9_-]+$";
            if (Regex.IsMatch(input, allowedPattern))
            {
                return input;
            }
            else
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(input);
                return Convert.ToBase64String(plainBytes)
                    .TrimEnd('=')          // Optional: make it more compact
                    .Replace('+', '-')     // URL-safe Base64 variant
                    .Replace('/', '_');
            }
        }

        /// <summary>
        /// Reverses the sanitization of a key back to its original plain text.
        /// The main goal is to decode a previously sanitized key.
        /// </summary>
        /// <param name="input">The sanitized key string.</param>
        /// <returns>The original plain text key.</returns>
        /// <remarks>
        /// Used when reading data from storage where keys were previously sanitized.
        /// </remarks>
        public static string DesanitizeKey(string input)
        {
            // If input is already valid (not encoded), return as-is
            string allowedPattern = @"^[a-zA-Z0-9_-]+$";
            if (!Regex.IsMatch(input, allowedPattern))
            {
                throw new ArgumentException("Invalid characters in key format", nameof(input));
            }

            try
            {
                // Reverse the URL-safe Base64 changes
                string base64 = input.Replace('-', '+').Replace('_', '/');

                // Pad with '=' to make it a valid base64 length
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                byte[] decodedBytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(decodedBytes);
            }
            catch (FormatException)
            {
                // Not a valid base64 string, return original input
                return input;
            }
        }
    }
}
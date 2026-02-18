using System.Text;
using System.Text.RegularExpressions;

namespace Utility.Encode
{
    public static class EncodeExtensions
    {
        public static bool IsValidEmail(string email)
        {
            // Simple regex pattern for email validation
            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return !string.IsNullOrWhiteSpace(email) && Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }
        
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
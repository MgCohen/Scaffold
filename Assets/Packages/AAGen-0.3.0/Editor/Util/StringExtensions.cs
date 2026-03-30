using System.Text.RegularExpressions;

namespace AAGen
{
    public static class StringExtensions
    {
        #region Static Methods
        /// <summary>
        /// Formats a class member name so that it is readable.
        /// </summary>
        /// <param name="input">The class member name.</param>
        /// <returns>The formatted name.</returns>
        /// <remarks>
        /// NOTE: Similar to <see cref="ObjectNames.NicifyVariableName"/>. Can it be replaced with this?
        /// </remarks>
        public static string ToReadableFormat(this string input)
        {
            // If the input string is invalid, then:
            if (string.IsNullOrEmpty(input))
            {
                // Return the input as-is. Do nothing else.
                return input;
            }

            // Otherwise, the input is valid.

            // Remove common Hungarian-style prefixes (m_ or k_)
            input = Regex.Replace(input, "^(m_|k_)", "");

            // Insert a space before each uppercase letter
            input = Regex.Replace(input, "(\\B[A-Z])", " $1");

            // Take the first letter, and force captitalization, and replace it in the string.
            return char.ToUpper(input[0]) + input.Substring(1);
        }
        
        /// <summary>
        /// Removes the file extension from a file name, if an extension exists.
        /// </summary>
        /// <param name="fileName">The file name to modify.</param>
        /// <returns>The file name without the extension.</returns>
        /// <remarks>
        /// NOTE: Similar to <see cref="System.IO.Path.GetFileNameWithoutExtension(string)"/>. Can it be replaced with this?
        /// </remarks>
        public static string RemoveExtension(this string fileName)
        {
            // If the file name is invalid, then:
            if (string.IsNullOrEmpty(fileName))
            {
                // Return the input as-is. Do nothing else.
                return fileName;
            }

            // Otherwise, the file name is valid.

            // Attempt to find the index of the file extension delimiter.
            int index = fileName.LastIndexOf('.');

            // If the index is positive non-zero, then it is a valid index.
            // If it is a valid index, then get the path excluding the delimiter and extension.
            // Otherwise, use the file name as-is.
            return index > 0 ? fileName.Substring(0, index) : fileName;
        }
        #endregion
    }
}
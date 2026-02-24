namespace Utility.Assert
{
    public static class Assert
    {
        public static bool IsNotNull<T>(T value) where T : class
        {
            if (value == null)
            {
                string paramName = nameof(value);
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null.");
            }
            return true;
        }

        public static bool IsNotEmpty(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{paramName} cannot be empty.", paramName);
            }
            return true;
        }

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
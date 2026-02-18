namespace Utility.Assert
{
    public static class Assert
    {
        public static void IsNotNull<T>(T value) where T : class
        {
            if (value == null)
            {
                string paramName = nameof(value);
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null.");
            }
        }

        public static void IsNotEmpty(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{paramName} cannot be empty.", paramName);
            }
        }

        public static void IsTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
#nullable enable
using System;
using System.Reflection;

namespace LiveOps.DTO.GameApi
{
    /// <summary>
    /// Resolves the wire <c>RequestKey</c> for a request DTO type.
    /// </summary>
    public static class GameApiKeyResolver
    {
        public static string GetKey(Type requestType)
        {
            if (requestType == null)
            {
                throw new ArgumentNullException(nameof(requestType));
            }

            GameApiKeyAttribute? attr = requestType.GetCustomAttribute<GameApiKeyAttribute>(inherit: true);
            if (attr == null)
            {
                throw new InvalidOperationException(
                    $"Request type '{requestType.FullName}' is missing [{nameof(GameApiKeyAttribute)}].");
            }

            return attr.Key;
        }
    }
}

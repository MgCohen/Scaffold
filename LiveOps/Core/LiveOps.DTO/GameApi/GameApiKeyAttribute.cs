using System;

namespace LiveOps.DTO.GameApi
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GameApiKeyAttribute : Attribute
    {
        public GameApiKeyAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key must be non-empty.", nameof(key));
            }

            Key = key;
        }

        public string Key { get; }
    }
}

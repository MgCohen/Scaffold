using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Utility.Json;

namespace GameModuleDTO.Json
{
    public static class JsonExtensions
    {
        // Create a single, reusable instance of our binder and settings
        private static readonly ISerializationBinder binder = new CrossPlatformTypeBinder();
        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings
        {
            SerializationBinder = binder,
            TypeNameHandling = TypeNameHandling.All,
            //For some reason when fetching data from unity api mix up the $type variable them this is needed
            MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead
        };

        /// <summary>
        /// Deserializes JSON using the robust cross-platform binder.
        /// </summary>
        public static T FromJson<T>(this string? json)
        {
            // Return directly if the string is a simple literal
            if (string.IsNullOrWhiteSpace(json) || (!json.TrimStart().StartsWith("{") && !json.TrimStart().StartsWith("[")))
            {
                // Attempt to cast if T is string
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)json;
                }
            }
            if (string.IsNullOrEmpty(json))
            {
                return default;
            }
            return JsonConvert.DeserializeObject<T>(json, settings);
        }

        /// <summary>
        /// Serializes an object using the robust cross-platform binder.
        /// </summary>
        public static string ToUnityJson(this object obj, Formatting formatting = Formatting.None)
        {
            if (obj == null)
            {
                return null;
            }

            // Create a copy of the settings to apply formatting dynamically
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                SerializationBinder = binder,
                TypeNameHandling = TypeNameHandling.All,
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = ShouldSerializeContractResolver.Instance
            };

            return JsonConvert.SerializeObject(obj, settings);
        }
    }
}
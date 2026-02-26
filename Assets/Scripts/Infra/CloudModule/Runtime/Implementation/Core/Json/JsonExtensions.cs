using Newtonsoft.Json;
using Utility.Json;

namespace Scaffold.CloudModules.Shared
{
    public static class JsonExtensions
    {
        private static readonly CrossPlatformTypeBinder crossPlatformTypeBinder = new  CrossPlatformTypeBinder();
        private static readonly ShouldSerializeContractResolver contractResolver = new ShouldSerializeContractResolver();
        
        /// <summary>
        /// Deserializes a JSON string into the given type.
        /// If the input is not valid JSON (i.e., doesn't start with '{', '[', or '"'),
        /// it returns the raw string as T, or tries to deserialize it as a literal.
        /// </summary>
        public static T FromJson<T>(this string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            string trimmed = json.Trim();

            // If it doesn't look like JSON, treat as raw value
            if (!(trimmed.StartsWith("{") || trimmed.StartsWith("[") || trimmed.StartsWith("\"")))
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)trimmed;
                }
            }

            try
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    SerializationBinder = crossPlatformTypeBinder,
                    TypeNameHandling = TypeNameHandling.Auto,
                };
                return JsonConvert.DeserializeObject<T>(json, settings);
            }
            catch
            {
                return default;
            }
        }

        public static string ToJson(this object obj, TypeNameHandling typeNameHandling = TypeNameHandling.All, Formatting formatting = Formatting.None)
        {
            if (obj == null)
            {
                return "";
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                SerializationBinder = crossPlatformTypeBinder,
                TypeNameHandling = typeNameHandling,
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = contractResolver
            };

            return JsonConvert.SerializeObject(obj, settings);
        }

        public static string ToSimpleJson(this object obj)
        {
            return ToJson(obj, TypeNameHandling.None);
        }
    }
}
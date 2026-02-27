using Newtonsoft.Json;
using Utility.Json;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// Provides extension methods for JSON serialization and deserialization.
    /// The main goal is to securely try to parse arbitrary strings into JSON using cross-platform capabilities.
    /// It is used heavily by the Cloud Code Service when passing messages and structured data payloads.
    /// </summary>
    public static class JsonExtensions
    {
        /// <summary>
        /// The static cross platform type binder for JSON evaluation.
        /// The main goal is to lazily provide the serialization binder globally.
        /// It is used as a setting context during serializations avoiding instantiation overhead.
        /// </summary>
        private static readonly CrossPlatformTypeBinder _crossPlatformTypeBinder = new CrossPlatformTypeBinder();

        /// <summary>
        /// The static contract resolver to dictate JSON mapping logic.
        /// The main goal is to guide how properties and fields map conditionally.
        /// It is used inside serialization settings.
        /// </summary>
        private static readonly ShouldSerializeContractResolver _contractResolver = new ShouldSerializeContractResolver();

        /// <summary>
        /// Extension to deserialize a JSON string into a target type.
        /// The main goal is to safely attempt parsing and fallback to defaults on error.
        /// It is used extensively when examining incoming network payloads.
        /// </summary>
        public static T FromJson<T>(this string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            string trimmed = json.Trim();

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
                    SerializationBinder = _crossPlatformTypeBinder,
                    TypeNameHandling = TypeNameHandling.Auto,
                };
                return JsonConvert.DeserializeObject<T>(json, settings);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Extension to serialize an object into a JSON string.
        /// The main goal is to convert arbitrary domain objects into transmit-ready strings.
        /// It is used immediately before dispatching requests to endpoints.
        /// </summary>
        public static string ToJson(this object obj, TypeNameHandling typeNameHandling = TypeNameHandling.All, Formatting formatting = Formatting.None)
        {
            if (obj == null)
            {
                return "";
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                SerializationBinder = _crossPlatformTypeBinder,
                TypeNameHandling = typeNameHandling,
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = _contractResolver
            };

            return JsonConvert.SerializeObject(obj, settings);
        }

        /// <summary>
        /// Extension to serialize an object with simplified serialization settings.
        /// The main goal is to compress to minimal type information.
        /// It is used when strict backend typings aren't strictly required or overhead is minimized.
        /// </summary>
        public static string ToSimpleJson(this object obj)
        {
            return ToJson(obj, TypeNameHandling.None);
        }
    }
}
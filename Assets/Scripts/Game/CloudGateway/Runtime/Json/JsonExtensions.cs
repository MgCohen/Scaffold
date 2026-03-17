using Newtonsoft.Json;
using Utility.Json;

namespace Scaffold.Game.CloudGateway
{
    /// <summary>
    /// Provides extension methods for JSON serialization and deserialization.
    /// The main goal is to securely try to parse arbitrary strings into JSON using cross-platform capabilities.
    /// It is used heavily by the Cloud Code Service when passing messages and structured data payloads.
    /// </summary>
    public static class JsonExtensions
    {
        private static readonly CrossPlatformTypeBinder crossPlatformTypeBinder = new CrossPlatformTypeBinder();

        private static readonly ShouldSerializeContractResolver contractResolver = new ShouldSerializeContractResolver();

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
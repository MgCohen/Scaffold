using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LiveOps.DTO.Json
{
    /// <summary>
    /// Shared JSON settings for DTOs round-tripping through Cloud Save and GameApi.
    /// </summary>
    public static class JsonExtensions
    {
        private static readonly ISerializationBinder Binder = new CrossPlatformTypeBinder();
        private static readonly JsonSerializerSettings ReadSettings = new()
        {
            SerializationBinder = Binder,
            TypeNameHandling = TypeNameHandling.All,
            MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead
        };

        /// <summary>Serializer contract shared with <see cref="GameApi" /> (Newtonsoft).</summary>
        public static JsonSerializer CreateGameApiSerializer() =>
            JsonSerializer.Create(new JsonSerializerSettings
            {
                SerializationBinder = Binder,
                TypeNameHandling = TypeNameHandling.Auto,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

        public static T FromJson<T>(this string json)
        {
            if (string.IsNullOrWhiteSpace(json) || (!json.TrimStart().StartsWith("{") && !json.TrimStart().StartsWith("[")))
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)json;
                }
            }

            if (string.IsNullOrEmpty(json))
            {
                return default;
            }

            return JsonConvert.DeserializeObject<T>(json, ReadSettings);
        }

        public static string ToJson(this object? obj, Formatting formatting = Formatting.None)
        {
            if (obj is null)
            {
                return null;
            }

            return JsonConvert.SerializeObject(
                obj,
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    Formatting = formatting,
                    NullValueHandling = NullValueHandling.Ignore
                });
        }
    }
}

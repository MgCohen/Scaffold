using Newtonsoft.Json;

namespace Utility.Json
{
    public static class JsonExtensions
    {
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

                try
                {
                    
                    string correctedJson = json.Replace("System.Private.CoreLib", "mscorlib");
                    return JsonConvert.DeserializeObject<T>($"\"{correctedJson}\"", new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto
                    });
                }
                catch
                {
                    return default;
                }
            }

            try
            {
                string correctedJson = json.Replace("System.Private.CoreLib", "mscorlib");
                return JsonConvert.DeserializeObject<T>(correctedJson, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Serializes an object into standard client-native JSON.
        /// </summary>
        public static string ToJson(this object obj, Formatting formatting = Formatting.None)
        {
            if (obj == null)
            {
                return null;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = ShouldSerializeContractResolver.Instance
            };

            return JsonConvert.SerializeObject(obj, settings);
        }

        /// <summary>
        /// Serializes an object into standard client-native JSON.
        /// </summary>
        public static string ToSimpleJson(this object obj)
        {
            if (obj == null)
            {
                return null;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = ShouldSerializeContractResolver.Instance
            };

            return JsonConvert.SerializeObject(obj, settings);
        }
    }
}
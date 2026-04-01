using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GameModuleDTO.Json
{
    /// <summary>
    /// Provides extension utilities for processing JSON payloads safely across network boundaries.
    /// </summary>
    /// <remarks>
    /// Automatically governs missing nodes during read states.
    /// </remarks>
    public static class JsonExtensions
    {
        // Create a single, reusable instance of our binder and settings
        private static readonly ISerializationBinder _binder = new CrossPlatformTypeBinder();

        /// <summary>
        /// Deserializes a predefined JSON package directly resolving embedded types safely.
        /// </summary>
        /// <typeparam name="T">The target generic parameter type.</typeparam>
        /// <param name="json">A standard JSON string literal.</param>
        /// <returns>An extracted representation assigned the desired underlying format.</returns>
        public static T FromJson<T>(this string json)
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

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                SerializationBinder = _binder,
                TypeNameHandling = TypeNameHandling.All,
                //For some reason when fetching data from unity api mix up the $type variable them this is needed
                MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead
            };

            return JsonConvert.DeserializeObject<T>(json, settings);
        }

        /// <summary>
        /// Serializes an object graph translating node definitions into JSON.
        /// </summary>
        /// <param name="obj">The memory class variable targeted for serialization.</param>
        /// <param name="formatting">The whitespace parameter string rules formatting style.</param>
        /// <returns>A formatting JSON string output representation.</returns>
        public static string ToJson(this object obj, Formatting formatting = Formatting.None)
        {
            if (obj == null)
            {
                return null;
            }

            // Create a copy of the settings to apply formatting dynamically
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(obj, settings);
        }
    }
}
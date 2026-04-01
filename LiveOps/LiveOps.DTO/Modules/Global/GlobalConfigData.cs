using GameModuleDTO.GameModule;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Global
{
    /// <summary>
    /// Configuration data for global game settings.
    /// </summary>
    public class GlobalConfigData : IGameModuleData
    {
        public string Key => typeof(GlobalConfigData).Name;

        [JsonProperty]
        private Dictionary<string, object> _values = new Dictionary<string, object>();

        [JsonIgnore]
        public Dictionary<string, object> Values => _values;

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (_values.TryGetValue(key, out object value))
            {
                try
                {
                    // Handle numeric types which might be deserialized as long or double by Newtonsoft.Json
                    if (typeof(T) == typeof(int))
                    {
                        return (T)(object)System.Convert.ToInt32(value);
                    }
                    if (typeof(T) == typeof(long))
                    {
                        return (T)(object)System.Convert.ToInt64(value);
                    }
                    if (typeof(T) == typeof(float))
                    {
                        return (T)(object)System.Convert.ToSingle(value);
                    }
                    if (typeof(T) == typeof(double))
                    {
                        return (T)(object)System.Convert.ToDouble(value);
                    }
                    return (T)value;
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        #region Specific Properties
        [JsonIgnore]
        public string SampleKey => GetValue("key", "value");

        [JsonIgnore]
        public int Number => GetValue("number", 6);

        [JsonIgnore]
        public bool Bool => GetValue("bool", true);

        [JsonIgnore]
        public string StringValue => GetValue("string", "hello");

        [JsonIgnore]
        public string Version => GetValue("version", "1.0.0");

        [JsonIgnore]
        public string Environment => GetValue("environment", "Production");

        [JsonIgnore]
        public int BeeEasyAttack => GetValue("bee_easy_attack", 10);
        #endregion

        public void SetValue(string key, object value)
        {
            _values[key] = value;
        }
    }
}

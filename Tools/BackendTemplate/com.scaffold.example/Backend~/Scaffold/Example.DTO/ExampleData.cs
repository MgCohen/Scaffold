using System;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.Keys;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Example
{
    [LiveOpsKey("ExampleData")]
    public sealed class ExampleData : IGameModuleData
    {
        [JsonProperty]
        private int _callCount;

        [JsonProperty]
        private string _sampleConfigValue = string.Empty;

        [JsonIgnore]
        public int CallCount => _callCount;

        [JsonIgnore]
        public string SampleConfigValue => _sampleConfigValue;

        [JsonConstructor]
        private ExampleData()
        {
        }

        public ExampleData(ExamplePersistence persistence, ExampleConfig config)
        {
            if (persistence == null)
            {
                throw new ArgumentNullException(nameof(persistence));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _callCount = persistence.CallCount;
            _sampleConfigValue = config.SampleConfigValue;
        }
    }
}

using LiveOps.DTO.Keys;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Example
{
    [LiveOpsKey("ExampleConfig")]
    public sealed class ExampleConfig
    {
        [JsonProperty]
        public string SampleConfigValue { get; set; } = "default";
    }
}

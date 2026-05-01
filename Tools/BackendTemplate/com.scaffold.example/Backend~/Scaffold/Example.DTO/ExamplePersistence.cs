using LiveOps.DTO.Keys;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Example
{
    [LiveOpsKey("ExamplePersistence")]
    public sealed class ExamplePersistence
    {
        [JsonProperty]
        public int CallCount { get; set; }
    }
}

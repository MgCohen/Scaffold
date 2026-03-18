using Newtonsoft.Json;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Request to complete a specific level.
    /// </summary>
    public class CompleteLevelRequest : ModuleRequest<CompleteLevelResponse>
    {
        [JsonProperty]
        public int LevelId { get; set; }

        public override void AssertModule()
        {
        }
    }
}

using Newtonsoft.Json;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Request to complete a specific level.
    /// </summary>
    public class CompleteLevelRequest : ModuleRequest<CompleteLevelResponse>
    {
        public CompleteLevelRequest(int levelId)
        {
            LevelId = levelId;
        }

        [JsonProperty]
        public int LevelId { get; set; }
    }
}

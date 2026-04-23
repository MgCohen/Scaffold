using LiveOps.Core.DTO.ModuleRequest;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.ModuleRequests
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

using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;
using Newtonsoft.Json;

namespace LiveOpsModules.DTO.ModuleRequests
{

    [UsesGameApi]
    [GameApiKey("CompleteLevel")]
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

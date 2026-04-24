using LiveOps.DTO.ModuleRequest;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    public class CompleteLevelRequest : ModuleRequest<CompleteLevelResponse>
    {
        public CompleteLevelRequest()
        {
        }

        public CompleteLevelRequest(int levelId)
        {
            LevelId = levelId;
        }

        [JsonProperty]
        public int LevelId { get; set; }
    }
}

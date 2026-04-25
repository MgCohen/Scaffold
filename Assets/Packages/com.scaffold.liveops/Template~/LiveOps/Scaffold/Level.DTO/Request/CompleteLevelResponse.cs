using LiveOps.DTO.ModuleRequest;
using LiveOps.Modules.DTO.Level;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.ModuleRequests
{

    public class CompleteLevelResponse : ModuleResponse
    {

        [JsonConstructor]
        public CompleteLevelResponse()
        {
        }

        public CompleteLevelResponse(bool succeeded, int? completedLevelId = null)
            : this(succeeded, null, completedLevelId)
        {
        }

        public CompleteLevelResponse(bool succeeded, LevelGameData data, int? completedLevelId = null)
        {
            Succeeded = succeeded;
            Data = data;
            CompletedLevelId = completedLevelId;
        }

        [JsonProperty("succeeded")]
        public bool Succeeded { get; protected set; }

        [JsonProperty("completedLevelId")]
        public int? CompletedLevelId { get; protected set; }

        [JsonProperty("data")]
        public LevelGameData Data { get; protected set; }
    }
}

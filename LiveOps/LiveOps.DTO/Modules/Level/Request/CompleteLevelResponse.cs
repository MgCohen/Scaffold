using GameModuleDTO.Modules.Level;
using Newtonsoft.Json;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Response for <see cref="CompleteLevelRequest"/>.
    /// </summary>
    public class CompleteLevelResponse : ModuleResponse
    {
        /// <summary>Newtonsoft deserialization (Cloud Code payloads use camelCase keys).</summary>
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

        /// <summary>Level ID that was persisted as completed when <see cref="Succeeded"/> is true.</summary>
        [JsonProperty("completedLevelId")]
        public int? CompletedLevelId { get; protected set; }

        /// <summary>
        /// Optional embedded level payload (e.g. for debugging or future use). The client applies progression via <see cref="CompletedLevelId"/> and <see cref="LevelGameData.ApplyCompletedLevel"/>.
        /// </summary>
        [JsonProperty("data")]
        public LevelGameData Data { get; protected set; }
    }
}

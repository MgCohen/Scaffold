using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Level
{
    /// <summary>
    /// One configured level ID and its derived availability for the client.
    /// </summary>
    public sealed class LevelStateEntry
    {
        [JsonConstructor]
        public LevelStateEntry(int levelId, LevelAvailabilityState state)
        {
            LevelId = levelId;
            State = state;
        }

        [JsonProperty]
        public int LevelId { get; }

        [JsonProperty]
        public LevelAvailabilityState State { get; }
    }
}

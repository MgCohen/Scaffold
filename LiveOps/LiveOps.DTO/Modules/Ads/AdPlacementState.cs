using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    public class AdPlacementState
    {
        [JsonProperty] public long LastAdWatchedAtUtcUnix { get; set; }
        [JsonProperty] public int WatchCount { get; set; }
    }
}

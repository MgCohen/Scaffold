using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Ads
{
    public class AdPlacementState
    {
        [JsonProperty] public long LastAdWatchedAtUtcUnix { get; set; }
        [JsonProperty] public int WatchCount { get; set; }
    }
}

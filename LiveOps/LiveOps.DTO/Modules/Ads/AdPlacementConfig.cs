using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    public class AdPlacementConfig
    {
        [JsonProperty] public float CooldownSeconds { get; set; } = 30f;
        [JsonProperty] public int MaxViews { get; set; } = 1;
        [JsonProperty] public string RewardType { get; set; } = string.Empty;
        [JsonProperty] public long RewardAmount { get; set; } = 0;
    }
}

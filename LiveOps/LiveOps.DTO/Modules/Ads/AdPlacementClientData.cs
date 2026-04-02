using System;
using System.Globalization;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    public class AdPlacementClientData
    {
        [JsonProperty] public float CooldownSeconds { get; set; }
        [JsonProperty] public string NextAdAvailableUtc { get; set; } = string.Empty;
        [JsonProperty] public int MaxViews { get; set; }
        [JsonProperty] public int WatchCount { get; set; }
        [JsonProperty] public bool HasReachedMaxViews { get; set; }
        [JsonProperty] public string RewardType { get; set; } = string.Empty;
        [JsonProperty] public long RewardAmount { get; set; }
        
        public bool IsAdAvailable()
        {
            if (HasReachedMaxViews) return false;
            
            if (string.IsNullOrEmpty(NextAdAvailableUtc)) return true;

            if (DateTime.TryParse(NextAdAvailableUtc, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime until))
            {
                return DateTime.UtcNow >= until;
            }

            return true;
        }

        public TimeSpan GetRemainingCooldown()
        {
            if (string.IsNullOrEmpty(NextAdAvailableUtc)) return TimeSpan.Zero;

            if (DateTime.TryParse(NextAdAvailableUtc, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime until))
            {
                TimeSpan diff = until - DateTime.UtcNow;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }

            return TimeSpan.Zero;
        }
    }
}

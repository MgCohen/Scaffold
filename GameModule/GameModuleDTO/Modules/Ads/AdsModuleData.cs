using System;
using System.Globalization;
using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    /// <summary>
    /// Data model for the Ads module.
    /// </summary>
    public class AdsModuleData : IGameModuleData
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public string Key { get { return GameDataExtensions.GetKey<AdsModuleData>(); } }

        [JsonProperty]
        private string _nextAdAvailableTime = string.Empty;

        /// <summary>Gets the ISO format string of the future time when an ad becomes available.</summary>
        [JsonIgnore]
        public string NextAdAvailableTime { get { return _nextAdAvailableTime; } }

        public void SetNextAdAvailableTime(float cooldownSeconds)
        {
            if (IsAdAvailable())
            {
                _nextAdAvailableTime = DateTime.UtcNow.AddSeconds(cooldownSeconds).ToString("O");
            }
        }

        public bool IsAdAvailable()
        {
            if (string.IsNullOrEmpty(_nextAdAvailableTime))
            {
                return true;
            }

            if (DateTime.TryParse(_nextAdAvailableTime, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime lastTime))
            {
                return DateTime.UtcNow >= lastTime;
            }

            return true;
        }

        public TimeSpan GetRemainingCooldown()
        {
            if (string.IsNullOrEmpty(_nextAdAvailableTime))
            {
                return TimeSpan.Zero;
            }

            if (DateTime.TryParse(_nextAdAvailableTime, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime lastTime))
            {
                TimeSpan diff = lastTime - DateTime.UtcNow;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }

            return TimeSpan.Zero;
        }
    }
}

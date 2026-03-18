using GameModuleDTO.Modules.Ads;
using Utility.Assert;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Response model for the WatchAd request.
    /// </summary>
    public class WatchAdResponse : ModuleResponse
    {
        public WatchAdResponse(AdsModuleData data)
        {
            Data = data;
        }

        /// <summary>Gets the updated ad module data.</summary>
        public AdsModuleData Data { get; protected set; }

        /// <summary>
        /// Validates that the returned data is not null.
        /// </summary>
        /// <returns>True if valid.</returns>
        public override bool IsValid()
        {
            return Assert.IsTrue(Data != null, $"{nameof(Data)} must not be null");
        }
    }
}

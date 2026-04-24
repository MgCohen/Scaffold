using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    /// <summary>
    /// Request initiating the ad watching process.
    /// </summary>
    [UsesGameApi]
    [GameApiKey("WatchAd")]
    public class WatchAdRequest : ModuleRequest<WatchAdResponse>
    {
        public string PlacementId { get; set; }
    }
}

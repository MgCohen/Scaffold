using LiveOps.Core.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    /// <summary>
    /// Request initiating the ad watching process.
    /// </summary>
    public class WatchAdRequest : ModuleRequest<WatchAdResponse>
    {
        public string PlacementId { get; set; }
    }
}

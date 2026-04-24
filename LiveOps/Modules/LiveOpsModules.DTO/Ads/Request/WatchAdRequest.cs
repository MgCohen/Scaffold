using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;

namespace LiveOpsModules.DTO.ModuleRequests
{

    [UsesGameApi]
    [GameApiKey("WatchAd")]
    public class WatchAdRequest : ModuleRequest<WatchAdResponse>
    {
        public string PlacementId { get; set; }
    }
}

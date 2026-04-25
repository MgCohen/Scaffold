using LiveOps.DTO.Keys;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    [LiveOpsKey("WatchAdRequest")]
    public class WatchAdRequest : ModuleRequest<WatchAdResponse>
    {        public string PlacementId { get; set; }
    }
}

using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    public class WatchAdRequest : ModuleRequest<WatchAdResponse>
    {
        public string PlacementId { get; set; }
    }
}

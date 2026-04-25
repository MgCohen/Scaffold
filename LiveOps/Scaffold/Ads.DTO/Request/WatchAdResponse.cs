using LiveOps.DTO.ModuleRequest;
using LiveOps.Modules.DTO.Ads;

namespace LiveOps.Modules.DTO.ModuleRequests
{

    public class WatchAdResponse : ModuleResponse
    {
        public WatchAdResponse(AdData data)
        {
            Data = data;
        }

        public AdData Data { get; protected set; }
    }
}

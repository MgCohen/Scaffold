using LiveOps.DTO.Keys;
using LiveOps.DTO.ModuleRequest;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    [LiveOpsKey("AddGoldRequest")]
    public class AddGoldRequest : ModuleRequest<LiveOps.Modules.DTO.Gold.GoldChangedResponse>
    {        public AddGoldRequest()
        {
        }

        public AddGoldRequest(long amount)
        {
            Amount = amount;
        }

        [JsonProperty]
        public long Amount { get; set; }
    }
}

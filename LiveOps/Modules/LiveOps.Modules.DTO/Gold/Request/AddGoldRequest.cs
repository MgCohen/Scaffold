using LiveOps.DTO.ModuleRequest;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    public class AddGoldRequest : ModuleRequest<LiveOps.Modules.DTO.Gold.GoldChangedResponse>
    {
        public AddGoldRequest()
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

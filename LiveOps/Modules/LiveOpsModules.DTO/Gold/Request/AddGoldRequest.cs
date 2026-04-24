using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;
using Newtonsoft.Json;

namespace LiveOpsModules.DTO.ModuleRequests
{
    [UsesGameApi]
    [GameApiKey("AddGold")]
    public class AddGoldRequest : ModuleRequest<LiveOpsModules.DTO.Gold.GoldChangedResponse>
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

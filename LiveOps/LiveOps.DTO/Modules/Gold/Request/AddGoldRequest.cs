using Newtonsoft.Json;

namespace GameModuleDTO.ModuleRequests
{
    public class AddGoldRequest : ModuleRequest<GameModuleDTO.Modules.Gold.GoldChangedResponse>
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

using LiveOps.DTO.ModuleRequest;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Gold
{

    public sealed class GoldChangedResponse : ModuleResponse
    {
        public GoldChangedResponse()
        {
        }

        public GoldChangedResponse(long newAmount, long diff)
        {
            NewAmount = newAmount;
            Diff = diff;
        }

        [JsonProperty]
        public long NewAmount { get; set; }

        [JsonProperty]
        public long Diff { get; set; }
    }
}

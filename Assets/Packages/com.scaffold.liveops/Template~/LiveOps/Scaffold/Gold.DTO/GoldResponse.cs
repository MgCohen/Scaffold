using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.Gold
{

    public class GoldResponse : ModuleResponse
    {
        public GoldResponse()
        {
        }

        public GoldResponse(long goldDelta)
        {
            GoldDelta = goldDelta;
        }

        public long GoldDelta { get; set; }
    }
}

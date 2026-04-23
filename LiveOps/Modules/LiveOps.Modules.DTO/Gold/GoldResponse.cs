using LiveOps.Core.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.Gold
{
    /// <summary>
    /// Response sent back when gold is added to a player.
    /// </summary>
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

using GameModuleDTO.ModuleRequests;

namespace GameModuleDTO.Modules.Gold
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
            SetResponse(ResponseStatusType.Success, "Gold added successfully.");
        }

        public long GoldDelta { get; set; }

        public override bool IsValid()
        {
            return true;
        }
    }
}

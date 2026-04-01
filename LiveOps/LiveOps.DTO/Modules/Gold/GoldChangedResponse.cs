using GameModuleDTO.ModuleRequests;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Gold
{
    /// <summary>
    /// Nested module response emitted when the server gold balance changes (e.g. after a reward).
    /// </summary>
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

        /// <summary>Gold total after the change (server persistence, clamped to config bounds).</summary>
        [JsonProperty]
        public long NewAmount { get; set; }

        /// <summary>Delta applied in this operation (may differ from actual balance change when clamped).</summary>
        [JsonProperty]
        public long Diff { get; set; }
    }
}

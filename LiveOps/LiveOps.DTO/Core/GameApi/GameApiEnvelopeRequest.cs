using Newtonsoft.Json.Linq;

namespace GameModuleDTO.GameApi
{
    /// <summary>
    /// Wire envelope for the single <c>GameApi</c> Cloud Code function.
    /// </summary>
    public sealed class GameApiEnvelopeRequest
    {
        /// <summary>Discriminator matching <see cref="ModuleRequest"/> concrete type name (e.g. <c>AddCurrencyRequest</c>).</summary>
        public string RequestKey { get; set; }

        /// <summary>Typed request body; deserialized using <c>RequestKey</c> on the server.</summary>
        public JObject Payload { get; set; }
    }
}

using Newtonsoft.Json.Linq;

namespace LiveOps.DTO.GameApi
{

    public sealed class GameApiEnvelopeRequest
    {

        public string RequestKey { get; set; }

        public JObject Payload { get; set; }
    }
}

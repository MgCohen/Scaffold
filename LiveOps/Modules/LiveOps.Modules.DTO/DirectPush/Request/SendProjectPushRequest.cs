using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{

    [UsesGameApi]
    [GameApiKey("SendProjectPush")]
    public class SendProjectPushRequest : ModuleRequest<SendPushResponse>
    {
        public string Message { get; set; }
        public string MessageType { get; set; }
        public string Guid { get; set; }
    }
}

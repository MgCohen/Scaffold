using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;

namespace LiveOpsModules.DTO.ModuleRequests
{

    [UsesGameApi]
    [GameApiKey("SendSelfPush")]
    public class SendSelfPushRequest : ModuleRequest<SendPushResponse>
    {
        public string Message { get; set; }
        public string MessageType { get; set; }
    }
}

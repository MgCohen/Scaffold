using LiveOps.DTO.Keys;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    [LiveOpsKey("SendSelfPushRequest")]
    public class SendSelfPushRequest : ModuleRequest<SendPushResponse>
    {        public string Message { get; set; }
        public string MessageType { get; set; }
    }
}

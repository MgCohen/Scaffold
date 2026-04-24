using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    public class SendSelfPushRequest : ModuleRequest<SendPushResponse>
    {
        public string Message { get; set; }
        public string MessageType { get; set; }
    }
}

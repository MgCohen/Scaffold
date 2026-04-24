using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    public class SendPlayerPushRequest : ModuleRequest<SendPushResponse>
    {
        public string Message { get; set; }
        public string MessageType { get; set; }
        public string PlayerId { get; set; }
        public string Guid { get; set; }
    }
}

using LiveOps.DTO.Keys;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    [LiveOpsKey("SendProjectPushRequest")]
    public class SendProjectPushRequest : ModuleRequest<SendPushResponse>
    {        public string Message { get; set; }
        public string MessageType { get; set; }
        public string Guid { get; set; }
    }
}

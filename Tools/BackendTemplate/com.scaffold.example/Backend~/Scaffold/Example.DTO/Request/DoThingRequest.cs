using LiveOps.DTO.Keys;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.Example
{
    [LiveOpsKey("DoThingRequest")]
    public class DoThingRequest : ModuleRequest<DoThingResponse>
    {
        public string Message { get; set; } = string.Empty;
    }
}

using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.Example
{
    public class DoThingResponse : ModuleResponse
    {
        public DoThingResponse(ExampleData data)
        {
            Data = data;
        }

        public ExampleData Data { get; protected set; }
    }
}

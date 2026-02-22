namespace GameModuleDTO.ModuleRequests
{
    public class IncrementCounterResponse : ModuleResponse
    {
        public int Value { get; protected set; }

        public IncrementCounterResponse(int value)
        {
            Value = value;
        }

        public override bool IsValid()
        {
            return true;
        }
    }
}
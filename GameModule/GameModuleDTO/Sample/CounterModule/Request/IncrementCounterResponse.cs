using GameModuleDTO.Sample.CounterModule;
using Utility.Assert;

namespace GameModuleDTO.ModuleRequests
{
    public class IncrementCounterResponse : ModuleResponse
    {
        public IncrementCounterResponse(int value)
        {
            Value = value;
        }

        public int Value { get; protected set; }

        public override bool IsValid()
        {
            return Assert.IsTrue(Value != 0, $"{nameof(Value)} must be different from 0");
        }
    }
}
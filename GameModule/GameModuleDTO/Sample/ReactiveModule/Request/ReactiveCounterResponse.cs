using GameModuleDTO.ModuleRequests;
using Utility.Assert;

namespace GameModuleDTO.Sample.ReactiveModule
{
    public class ReactiveCounterResponse : ModuleResponse
    {
        public ReactiveCounterResponse(int value)
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
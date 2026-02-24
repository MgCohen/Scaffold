using GameModuleDTO.ModuleRequests;
using Utility.Assert;

namespace GameModuleDTO.Sample.ReactiveModule
{
    public class ReactiveCounterRequest : ModuleRequestT<ReactiveCounterResponse>
    {
        public int Value { get; set; }

        public override void AssertModule()
        {
            Assert.IsTrue(Value != 0, $"{nameof(Value)} must be different from 0");
        }
    }
}

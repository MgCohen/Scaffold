using GameModuleDTO.ModuleRequests;
using Scaffold.Tools.Assert;

namespace GameModuleDTO.Sample.ReactiveModule
{
    /// <summary>
    /// Sample implementation managing reactive parameter commands execution blocks.
    /// </summary>
    public class ReactiveCounterRequest : ModuleRequestT<ReactiveCounterResponse>
    {
        /// <summary>Gets or sets the tracking token internally securely mapping parameters.</summary>
        public int Value { get; set; }

        /// <summary>
        /// Checks network boundaries natively logically cleanly.
        /// </summary>
        public override void AssertModule()
        {
            Assert.IsTrue(Value != 0, $"{nameof(Value)} must be different from 0");
        }
    }
}

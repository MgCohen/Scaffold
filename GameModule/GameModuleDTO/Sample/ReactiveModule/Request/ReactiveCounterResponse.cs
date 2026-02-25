using GameModuleDTO.ModuleRequests;
using Utility.Assert;

namespace GameModuleDTO.Sample.ReactiveModule
{
    /// <summary>
    /// Sample payload returning active state changes for reactive components.
    /// </summary>
    public class ReactiveCounterResponse : ModuleResponse
    {
        /// <summary>
        /// Initializes the returned response cleanly parameterizing the internal value.
        /// </summary>
        /// <param name="value">The resulting numeric execution argument.</param>
        public ReactiveCounterResponse(int value)
        {
            Value = value;
        }

        /// <summary>Gets the bound execution parameter integer internally.</summary>
        public int Value { get; protected set; }

        /// <summary>
        /// Validates that the response correctly contains valid processing state data.
        /// </summary>
        /// <returns>True if validation checks succeed effectively.</returns>
        public override bool IsValid()
        {
            return Assert.IsTrue(Value != 0, $"{nameof(Value)} must be different from 0");
        }
    }
}
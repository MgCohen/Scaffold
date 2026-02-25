using GameModuleDTO.Sample.CounterModule;
using Utility.Assert;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Sample model returned validating the network integer accumulation natively.
    /// </summary>
    public class IncrementCounterResponse : ModuleResponse
    {
        /// <summary>
        /// Initializes the returned integer wrapper directly.
        /// </summary>
        /// <param name="value">The tracking numeric parameter.</param>
        public IncrementCounterResponse(int value)
        {
            Value = value;
        }

        /// <summary>Gets the internally recorded value literal explicitly safely.</summary>
        public int Value { get; protected set; }

        /// <summary>
        /// Asserts the returned wrapper represents an active tracking shift completely.
        /// </summary>
        /// <returns>True if the condition succeeds.</returns>
        public override bool IsValid()
        {
            return Assert.IsTrue(Value != 0, $"{nameof(Value)} must be different from 0");
        }
    }
}
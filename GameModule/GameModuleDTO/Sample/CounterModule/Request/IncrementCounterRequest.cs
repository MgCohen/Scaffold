using Utility.Assert;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Sample request initiating a numeric counter step progression correctly.
    /// </summary>
    public class IncrementCounterRequest : ModuleRequestT<IncrementCounterResponse>
    {
        /// <summary>
        /// Bootstraps the numeric step request efficiently directly.
        /// </summary>
        /// <param name="authKey">The target execution parameter.</param>
        public IncrementCounterRequest(string authKey)
        {
            AuthKey = authKey;
        }

        /// <summary>
        /// Tests the target keys making sure the caller holds specific tokens correctly.
        /// </summary>
        public override void AssertModule()
        {
            Assert.IsNotNull(AuthKey);
            Assert.IsNotEmpty(AuthKey, nameof(AuthKey));
        }
    }
}
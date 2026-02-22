using Utility.Assert;

namespace GameModuleDTO.ModuleRequests
{
    public class IncrementCounterRequest : ModuleRequestT<IncrementCounterResponse>
    {
        public IncrementCounterRequest(string authKey)
        {
            AuthKey = authKey;
        }

        public override void AssertModule()
        {
            Assert.IsNotNull(AuthKey);
            Assert.IsNotEmpty(AuthKey, nameof(AuthKey));
        }
    }
}
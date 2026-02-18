using Utility.Assert;

namespace GameModuleDTO.ModuleRequests
{
    public class GameDataRequest : ModuleRequestT<GameDataResponse>
    {
        public string[] ModuleKeys { get; private set; }
        
        public GameDataRequest(string authKey, params string[] moduleKeys)
        {
            AuthKey = authKey;
            ModuleKeys = moduleKeys;
        }

        public override void AssertModule()
        {
            Assert.IsNotNull(ModuleKeys);
            foreach (string moduleKey in ModuleKeys)
            {
                Assert.IsNotNull(moduleKey);
            }
        }
    }
}
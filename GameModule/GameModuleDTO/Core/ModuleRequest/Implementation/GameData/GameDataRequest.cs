using Utility.Assert;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Represents a network request asking for specific module data to execute logic.
    /// </summary>
    public class GameDataRequest : ModuleRequestT<GameDataResponse>
    {
        /// <summary>Gets the targeted keys associated with the loaded modules.</summary>
        public string[] ModuleKeys { get; private set; }

        /// <summary>
        /// Initializes a new instance of the request.
        /// </summary>
        /// <param name="authKey">The required authorization key.</param>
        /// <param name="moduleKeys">A collection of keys representing expected modules.</param>
        public GameDataRequest(string authKey, params string[] moduleKeys)
        {
            AuthKey = authKey;
            ModuleKeys = moduleKeys;
        }

        /// <summary>
        /// Validates the internal state of the module keys checking for null elements properly.
        /// </summary>
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
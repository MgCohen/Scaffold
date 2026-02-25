namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Defines a request starting the lifecycle for the network modules specifically.
    /// </summary>
    public class InitializeGameModulesRequest : ModuleRequestT<GameDataResponse>
    {
        /// <summary>
        /// Initializes the module startup explicitly.
        /// </summary>
        /// <param name="authKey">The necessary network credentials natively.</param>
        public InitializeGameModulesRequest(string authKey)
        {
            AuthKey = authKey;
        }

        /// <summary>
        /// Validates payload execution constraints.
        /// </summary>
        public override void AssertModule()
        {

        }
    }
}
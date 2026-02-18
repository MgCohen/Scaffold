namespace GameModuleDTO.ModuleRequests
{
    public class InitializeGameModulesRequest : ModuleRequestT<GameDataResponse>
    {
        public InitializeGameModulesRequest(string authKey)
        {
            AuthKey = authKey;
        }

        public override void AssertModule()
        {
            
        }
    }
}
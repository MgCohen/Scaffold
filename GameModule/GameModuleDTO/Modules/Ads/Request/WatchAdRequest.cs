namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Request initiating the ad watching process.
    /// </summary>
    public class WatchAdRequest : ModuleRequest<WatchAdResponse>
    {
        /// <summary>
        /// Ensures the auth key is provided.
        /// </summary>
        public override void AssertModule()
        {

        }
    }
}

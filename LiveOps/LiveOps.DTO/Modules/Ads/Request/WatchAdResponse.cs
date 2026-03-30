using GameModuleDTO.Modules.Ads;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Response model for the watch-ad request.
    /// </summary>
    public class WatchAdResponse : ModuleResponse
    {
        public WatchAdResponse(AdData data)
        {
            Data = data;
        }

        /// <summary>Gets the updated ads payload.</summary>
        public AdData Data { get; protected set; }
    }
}

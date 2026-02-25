using Unity.Services.CloudCode.Apis;

namespace GameModule.Initialize
{
    /// <summary>
    /// Handles statically mapped references to global Unity interfaces.
    /// </summary>
    public static class ModuleServices
    {
        /// <summary>Globally mapped interface referencing the internal Unity Game API statically.</summary>
        public static IGameApiClient GameApiClient;
    }
}
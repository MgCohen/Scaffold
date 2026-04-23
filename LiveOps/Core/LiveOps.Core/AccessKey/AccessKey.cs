using System.Threading.Tasks;
using LiveOps.Core.ModuleFetchData;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Core.AccessKey
{
    /// <summary>
    /// Validates server-to-server access via a shared GUID stored in GameState.
    /// </summary>
    public static class AccessKey
    {
        /// <summary>
        /// Validates the provided GUID against the stored server access key.
        /// </summary>
        /// <param name="gameState">The game state data source.</param>
        /// <param name="context">The current execution context.</param>
        /// <param name="guid">The GUID to validate.</param>
        /// <returns>True if the GUID matches the stored access key.</returns>
        public static async Task<bool> ValidServer(IGameState gameState, IExecutionContext context, string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            string storedKey = await gameState.Get<string>(context, "AccessKey", "ServerKey", string.Empty);
            return storedKey == guid;
        }
    }
}

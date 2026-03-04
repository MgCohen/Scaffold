using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Keys;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameModule
{
    /// <summary>
    /// Represents static utilities extracting core module definitions safely.
    /// </summary>
    public static class GameModuleExtensions
    {
        #region GameData

        /// <summary>
        /// Obtains the Unity administrative access token safely.
        /// </summary>
        /// <param name="gameState">The target configuration execution instance.</param>
        /// <param name="context">The underlying execution framework instance.</param>
        /// <returns>A predefined authorization payload correctly.</returns>
        public static async Task<string> GetUnityAuth(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.UnityToken);
        }

        /// <summary>
        /// Pulls the cloud administrative function identification explicitly.
        /// </summary>
        /// <param name="gameState">The active database instance map.</param>
        /// <param name="context">The active session processing instance details.</param>
        /// <returns>An extracted identification definition explicitly securely.</returns>
        public static async Task<string> GetAdminFunctionsKeyId(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.AdminFunctionsKey);
        }

        /// <summary>
        /// Safely evaluates and extracts cloud access strings.
        /// </summary>
        /// <param name="gameState">The tracking identifier.</param>
        /// <param name="context">The operating instance.</param>
        /// <returns>A specific payload directly.</returns>
        public static async Task<string> GetAdminFunctionsSecretKey(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.AdminFunctionsSecretKey);
        }
        #endregion
    }
}

using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Keys;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameModule
{
    public static class GameModuleExtensions
    {
        #region GameData
        public static async Task<string> GetUnityAuth(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.UnityToken);
        }

        public static async Task<string> GetAdminFunctionsKeyId(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.AdminFunctionsKey);
        }

        public static async Task<string> GetAdminFunctionsSecretKey(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.AdminFunctionsSecretKey);
        }
        #endregion
    }
}
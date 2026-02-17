using System;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using Unity.Services.CloudCode.Core;

namespace GameModule.AccessKey
{
    public static class AccessKey
    {
        public static async Task<bool> ValidServer(GameState gameState, IExecutionContext context, string guid)
        {
            string unityAuth = await GetUnityAuth(gameState, context);
            if (unityAuth != guid)
            {
                throw new UnauthorizedAccessException("Not Authorized");
                return false;
            }
            return true;
        }

        public static async Task<string> GetUnityAuth(GameState gameState, IExecutionContext context)
        {
            return await gameState.GetUnityAuth(context);
        }
    }
}
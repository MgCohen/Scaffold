using System;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using Unity.Services.CloudCode.Core;

namespace GameModule.AccessKey
{
    public static class AccessKey
    {
        public static async Task<string> GetUnityAuth(this IExecutionContext context, GameState gameState)
        {
            return await gameState.GetUnityAuth(context);
        }
        
        public static async Task<bool> GetValidAuth(this IExecutionContext context, GameState gameState, string auth)
        {
            if (string.IsNullOrEmpty(auth))
            {
                return false;
            }
            string unityAuth = await GetUnityAuth(context, gameState);
            return unityAuth == auth;
        }
        
        public static async Task<bool> ValidateAuth(this IExecutionContext context, GameState gameState, string auth)
        {
            bool valid = await context.GetValidAuth(gameState, auth);
            return valid ? true : throw new UnauthorizedAccessException("Not Authorized");
        }
    }
}
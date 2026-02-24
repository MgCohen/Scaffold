using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    public static class PlayerDataExtensions
    {
        public static async Task Set(this PlayerData playerData, IExecutionContext context, IGameModuleData value, bool useWriteLock = false)
        {
            await playerData.Set(context, value.Key, value, useWriteLock);
        }

        public static void AddToCache(this PlayerData playerData, IGameModuleData moduleData)
        {
            if (moduleData == null)
            {
                return;
            }
            playerData.AddToCache(moduleData.Key);
        }
    }
}

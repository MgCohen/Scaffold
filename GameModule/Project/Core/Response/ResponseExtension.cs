using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameModule.ModuleFetchData;
using GameModuleDTO.ModuleRequests;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace GameModule.Response
{
    public static class ResponseExtension
    {
        public static async Task<T> ResolveResponse<T>(this ModuleRequestT<T> request, T response, IExecutionContext context, PlayerData playerData) where T : ModuleResponse
        {
            if (request == null)
            {
                return null;
            }
            if (context != null && playerData != null && response.GameModuleDatas.Any())
            {
                List<SetItemBody> items = response.GameModuleDatas.Select(moduleData => new SetItemBody(moduleData.Key, moduleData)).ToList();
                await playerData.SetBatch(context, items);
            }
            response.ClearGameModuleDatas();
            return response;
        }
    }
}
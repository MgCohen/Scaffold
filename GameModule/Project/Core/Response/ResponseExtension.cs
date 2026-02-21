using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameModule.ModuleFetchData;
using GameModuleDTO.Json;
using GameModuleDTO.ModuleRequests;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace GameModule.Response
{
    //TODO: Review
    public static class ResponseExtension
    {
        public static async Task<string> Resolve(this ModuleResponse response, IExecutionContext context, PlayerData playerData)
        {
            if (!response.GameModuleDatas.Any())
            {
                List<SetItemBody> items = response.GameModuleDatas.Select(moduleData => new SetItemBody(moduleData.Key, moduleData)).ToList();
                await playerData.SetBatch(context, items);
            }
            return response.ToJson();
        }
    }
}
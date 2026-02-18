using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameModule.ModuleFetchData;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.Json;
using Unity.Services.CloudSave.Model;
using Utility.List;

namespace GameModule.Response
{
    using Response = GameModuleDTO.Response.Response;
    
    public static class ResponseExtension
    {
        public static async Task<string> Resolve(this Response response, IExecutionContext context, PlayerData playerData)
        {
            if (!response.modules.Any())
            {
                List<SetItemBody> items = response.modules.Select(moduleData => new SetItemBody(moduleData.Key, moduleData)).ToList();
                await playerData.SetBatch(context, items);
            }
            return response.ToUnityJson();
        }
    }
}
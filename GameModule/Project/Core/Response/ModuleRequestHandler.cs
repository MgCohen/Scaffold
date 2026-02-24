using GameModuleDTO.ModuleRequests;
using GameModule.Signal;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;
using GameModule.ModuleFetchData;

namespace GameModule.Response
{
    public class ModuleRequestHandler
    {
        public ModuleRequestHandler(SignalModule signalModule, PlayerData playerData) //TODO: Add gameData
        {
            _signalModule = signalModule;
            _playerData = playerData;
        }

        private readonly SignalModule _signalModule;
        private readonly PlayerData _playerData;
        public ModuleRequest Request { get; private set; }
        public List<ModuleResponse> Responses { get; protected set; } = new List<ModuleResponse>();

        public void SetCurrentRequest(ModuleRequest request)
        {
            Request = request;
        }

        public void NotifyRequestResolve(ModuleRequest request)
        {
            if (request == null)
            {
                return;
            }
            _signalModule.Push(request);
        }

        public async Task<T> ResolveResponse<T>(ModuleRequestT<T> request, T response, IExecutionContext context, PlayerData playerData = null) where T : ModuleResponse
        {
            if (request == null || context == null)
            {
                return null;
            }

            NotifyRequestResolve(request);
            if (playerData != null)
            {
                await playerData.SaveCache(context);
            }
            else
            {
                await _playerData.SaveCache(context);
            }

            return response;
        }

        public void AddResponse(ModuleResponse response)
        {
            if (response == null)
            {
                return;
            }

            Responses.Add(response);
        }
    }
}
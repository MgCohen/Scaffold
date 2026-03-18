using GameModuleDTO.ModuleRequests;
using GameModule.Signal;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;
using GameModule.ModuleFetchData;


namespace GameModule.Response
{
    /// <summary>
    /// Processes incoming module queries and formats responses.
    /// </summary>
    public class ModuleRequestHandler
    {
        public ModuleRequestHandler(SignalModule signalModule, IPlayerData playerData) //TODO: Add gameData
        {
            _signalModule = signalModule;
            _playerData = playerData;
        }

        private readonly SignalModule _signalModule;
        private readonly IPlayerData _playerData;
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

        public async Task<T> ResolveResponse<T>(IExecutionContext context, ModuleRequest<T> request, T response, IPlayerData playerData = null) where T : ModuleResponse
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
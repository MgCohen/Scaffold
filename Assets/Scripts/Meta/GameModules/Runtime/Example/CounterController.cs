using System.Threading.Tasks;
using GameModuleDTO.Sample.CounterModule;
using GameModuleDTO.ModuleRequests;
using Scaffold.CloudGateway;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class CounterController : GameModule<CounterModuleData>
    {
        public ICloudGatewayAuthKey iCloudGatewayAuthKey;
        
        protected override async Task OnInitialize(CounterModuleData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(CounterModuleData gameModuleData)
        {
            GameDebug.Log($"Counter updated: {gameModuleData.Value}", "CounterController");
            await Task.Yield();
        }

        public async Task IncrementCounter()
        {
            IncrementCounterRequest request = new IncrementCounterRequest(iCloudGatewayAuthKey.Guid);
            IncrementCounterResponse response = await cloudService.CallEndpointAsync(request);
            GameDebug.Log($"Counter incremented. New value: {response.Value}", "CounterController");
        }
    }
}
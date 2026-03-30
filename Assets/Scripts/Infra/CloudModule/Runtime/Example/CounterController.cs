using GameModuleDTO.Sample.CounterModule;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;
using UnityEngine;

namespace Scaffold.CloudModules.Example
{
    public class CounterController : GameModuleT<CounterModuleData>
    {
        protected override async Awaitable OnInitialize(CounterModuleData gameModuleData)
        {
            await Awaitable.NextFrameAsync();
        }

        protected override async Awaitable OnUpdateData(CounterModuleData gameModuleData)
        {
            GameDebug.Log($"Counter updated: {gameModuleData.Value}", "CounterController");
            await Awaitable.NextFrameAsync();
        }

        public async Awaitable IncrementCounter()
        {
            IncrementCounterRequest request = new IncrementCounterRequest(GameModuleAuthKey.guid);
            IncrementCounterResponse response = await _cloudCodeService.CallEndpointAsync(request);
            GameDebug.Log($"Counter incremented. New value: {response.Value}", "CounterController");
        }
    }
}
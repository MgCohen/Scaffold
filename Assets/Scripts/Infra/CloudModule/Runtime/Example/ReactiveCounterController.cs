using GameModuleDTO.Sample.ReactiveModule;
using Scaffold.Logging;
using UnityEngine;

namespace Scaffold.CloudModules.Example
{
    public class ReactiveCounterController : GameModuleT<ReactiveModuleData>
    {
        protected override async Awaitable OnInitialize(ReactiveModuleData gameModuleData)
        {
            _cloudCodeService.SubscribeToResponse<ReactiveCounterResponse>(OnReactiveCounterResponse);
            await Awaitable.NextFrameAsync();
        }

        private async Awaitable OnReactiveCounterResponse(ReactiveCounterResponse response)
        {
            GameDebug.Log($"Reactive Counter received update: {response.ValueB}", "ReactiveCounterController");
            Data.valueB = response.ValueB;
            await OnUpdateData(Data);
        }

        private void OnDestroy()
        {
            _cloudCodeService.UnsubscribeFromResponse<ReactiveCounterResponse>(OnReactiveCounterResponse);
        }

        protected override async Awaitable OnUpdateData(ReactiveModuleData gameModuleData)
        {
            GameDebug.Log($"Reactive Counter updated", "ReactiveCounterController");
            await Awaitable.NextFrameAsync();
        }
    }
}
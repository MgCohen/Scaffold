using System.Threading.Tasks;
using GameModuleDTO.Sample.ReactiveModule;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class ReactiveCounterController : GameModule<ReactiveModuleData>
    {
        protected override async Task OnInitialize(ReactiveModuleData gameModuleData)
        {
            cloudService.SubscribeToResponse<ReactiveCounterResponse>(OnReactiveCounterResponse);
            await Task.Yield();
        }

        private async Task OnReactiveCounterResponse(ReactiveCounterResponse response)
        {
            GameDebug.Log($"Reactive Counter received update: {response.ValueB}", "ReactiveCounterController");
            Data.valueB = response.ValueB;
            await OnUpdateData(Data);
        }

        private void OnDestroy()
        {
            cloudService.UnsubscribeFromResponse<ReactiveCounterResponse>(OnReactiveCounterResponse);
        }

        protected override async Task OnUpdateData(ReactiveModuleData gameModuleData)
        {
            GameDebug.Log($"Reactive Counter updated", "ReactiveCounterController");
            await Task.Yield();
        }
    }
}
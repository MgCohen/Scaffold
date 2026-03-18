using System.Threading.Tasks;
using GameModuleDTO.Modules.Ads;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class AdsController : GameModule<AdsModuleData>
    {
        protected override async Task OnInitialize(AdsModuleData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(AdsModuleData gameModuleData)
        {
            GameDebug.Log($"Ads data updated. Next ad available: {gameModuleData.NextAdAvailableTime}", "AdsController");
            await Task.Yield();
        }

        public async Task WatchAd()
        {
            WatchAdRequest request = new WatchAdRequest();
            WatchAdResponse response = await cloudService.CallEndpointAsync(request);
            UpdateData(response.Data);
            GameDebug.Log($"Ad watched. Next available at: {response.Data.NextAdAvailableTime}", "AdsController");
        }
    }
}

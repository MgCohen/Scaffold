using System.Threading.Tasks;
using GameModuleDTO.Modules.Ads;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;
using VContainer;

namespace Scaffold.GameModules
{
    public class AdsController : GameModule<AdsModuleData>
    {
        [Inject]
        protected AdsConfigController configController;

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
            if (!IsAdAvailable())
            {
                return;
            }

            WatchAdRequest request = new WatchAdRequest();
            WatchAdResponse response = await cloudService.CallEndpointAsync(request);
            UpdateData(response.Data);
            GameDebug.Log($"Ad watched. Next available at: {response.Data.NextAdAvailableTime}", "AdsController");
        }

        public bool IsAdAvailable()
        {
            if (!configController.Data.IsActive)
            {
                GameDebug.Log("Ads are currently disabled.", "AdsController");
                return false;
            }

            if (!Data.IsAdAvailable())
            {
                GameDebug.Log("Ad is not available yet (cooldown).", "AdsController");
                return false;
            }

            return true;
        }
    }
}

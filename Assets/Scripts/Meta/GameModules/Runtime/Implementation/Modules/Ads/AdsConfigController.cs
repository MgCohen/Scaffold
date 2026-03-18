using System.Threading.Tasks;
using GameModuleDTO.Modules.Ads;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class AdsConfigController : GameModule<AdsConfigData>
    {
        protected override async Task OnInitialize(AdsConfigData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(AdsConfigData gameModuleData)
        {
            GameDebug.Log("Ads Config data updated.", "AdsConfigController");
            await Task.Yield();
        }
    }
}

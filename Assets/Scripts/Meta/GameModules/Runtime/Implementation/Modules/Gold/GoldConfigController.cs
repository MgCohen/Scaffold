using System.Threading.Tasks;
using GameModuleDTO.Modules.Gold;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class GoldConfigController : GameModule<GoldConfigData>
    {
        protected override async Task OnInitialize(GoldConfigData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(GoldConfigData gameModuleData)
        {
            GameDebug.Log("Gold Config data updated.", "GoldConfigController");
            await Task.Yield();
        }
    }
}

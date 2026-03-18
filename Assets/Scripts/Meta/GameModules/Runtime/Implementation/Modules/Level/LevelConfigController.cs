using System.Threading.Tasks;
using GameModuleDTO.Modules.Level;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class LevelConfigController : GameModule<LevelConfigData>
    {
        protected override async Task OnInitialize(LevelConfigData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(LevelConfigData gameModuleData)
        {
            GameDebug.Log("Level Config data updated.", "LevelConfigController");
            await Task.Yield();
        }
    }
}

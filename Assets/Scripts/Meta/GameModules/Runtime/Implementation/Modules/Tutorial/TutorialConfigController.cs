using System.Threading.Tasks;
using GameModuleDTO.Modules.Tutorial;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class TutorialConfigController : GameModule<TutorialConfigData>
    {
        protected override async Task OnInitialize(TutorialConfigData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(TutorialConfigData gameModuleData)
        {
            GameDebug.Log("Tutorial Config data updated.", "TutorialConfigController");
            await Task.Yield();
        }
    }
}

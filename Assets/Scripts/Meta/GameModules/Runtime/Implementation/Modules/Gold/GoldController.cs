using System.Threading.Tasks;
using GameModuleDTO.Modules.Gold;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class GoldController : GameModule<GoldModuleData>
    {
        protected override async Task OnInitialize(GoldModuleData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(GoldModuleData gameModuleData)
        {
            GameDebug.Log($"Gold updated. New balance: {gameModuleData.Current}", "GoldController");
            await Task.Yield();
        }
    }
}

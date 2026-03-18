using System.Threading.Tasks;
using GameModuleDTO.Modules.Global;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class GlobalConfigController : GameModule<GlobalConfigData>
    {
        protected override async Task OnInitialize(GlobalConfigData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(GlobalConfigData gameModuleData)
        {
            GameDebug.Log("Global Config data updated.", "GlobalConfigController");
            await Task.Yield();
        }

        public string Version => Data.Version;
        public string Environment => Data.Environment;
        public int BeeEasyAttack => Data.BeeEasyAttack;

        public T GetValue<T>(string key, T defaultValue = default)
        {
            return Data.GetValue<T>(key, defaultValue);
        }
    }
}

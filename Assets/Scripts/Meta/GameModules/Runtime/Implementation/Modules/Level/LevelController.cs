using System.Threading.Tasks;
using GameModuleDTO.Modules.Level;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class LevelController : GameModule<LevelModuleData>
    {
        protected override async Task OnInitialize(LevelModuleData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(LevelModuleData gameModuleData)
        {
            GameDebug.Log($"Level data updated. Completed levels: {gameModuleData.Progress.Count}", "LevelController");
            await Task.Yield();
        }

        public async Task CompleteLevel(int levelId)
        {
            CompleteLevelRequest request = new CompleteLevelRequest(levelId);
            CompleteLevelResponse response = await cloudService.CallEndpointAsync(request);
            UpdateData(response.Data);
            GameDebug.Log($"Level {levelId} completed. Progress updated.", "LevelController");
        }
    }
}

using System.Linq;
using System.Threading.Tasks;
using GameModuleDTO.Modules.Level;
using GameModuleDTO.Modules.Common;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;
using VContainer;

namespace Scaffold.GameModules
{
    public class LevelController : GameModule<LevelModuleData>
    {
        [Inject]
        protected LevelConfigController configController;

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
            if (!CanCompleteLevel(levelId))
            {
                return;
            }

            CompleteLevelRequest request = new CompleteLevelRequest(levelId);
            CompleteLevelResponse response = await cloudService.CallEndpointAsync(request);
            UpdateData(response.Data);
            GameDebug.Log($"Level {levelId} completed. Progress updated.", "LevelController");
        }

        public bool CanCompleteLevel(int levelId)
        {
            if (!configController.Data.IsActive)
            {
                GameDebug.Log("Levels are currently disabled.", "LevelController");
                return false;
            }

            if (Data.IsCompleted(levelId.ToString()))
            {
                GameDebug.Log($"Level {levelId} is already completed.", "LevelController");
                return false;
            }

            return true;
        }
    }
}

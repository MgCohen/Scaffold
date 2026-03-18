using System.Linq;
using System.Threading.Tasks;
using GameModuleDTO.Modules.Tutorial;
using GameModuleDTO.Modules.Common;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;
using VContainer;

namespace Scaffold.GameModules
{
    public class TutorialController : GameModule<TutorialModuleData>
    {
        [Inject]
        protected TutorialConfigController configController;

        protected override async Task OnInitialize(TutorialModuleData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(TutorialModuleData gameModuleData)
        {
            GameDebug.Log($"Tutorial data updated. Steps completed: {gameModuleData.Progress.Count}", "TutorialController");
            await Task.Yield();
        }

        public async Task CompleteTutorial(int tutorialId)
        {
            if (!CanCompleteTutorial(tutorialId))
            {
                return;
            }

            CompleteTutorialRequest request = new CompleteTutorialRequest(tutorialId);
            CompleteTutorialResponse response = await cloudService.CallEndpointAsync(request);
            UpdateData(response.Data);
            GameDebug.Log($"Tutorial step {tutorialId} completed.", "TutorialController");
        }

        public bool CanCompleteTutorial(int tutorialId)
        {
            if (!configController.Data.IsActive)
            {
                GameDebug.Log("Tutorials are currently disabled.", "TutorialController");
                return false;
            }

            if (Data.IsCompleted(tutorialId.ToString()))
            {
                GameDebug.Log($"Tutorial step {tutorialId} is already completed.", "TutorialController");
                return false;
            }

            return true;
        }
    }
}

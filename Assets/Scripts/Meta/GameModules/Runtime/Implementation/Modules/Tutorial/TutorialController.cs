using System.Threading.Tasks;
using GameModuleDTO.Modules.Tutorial;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class TutorialController : GameModule<TutorialModuleData>
    {
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
            CompleteTutorialRequest request = new CompleteTutorialRequest(tutorialId);
            CompleteTutorialResponse response = await cloudService.CallEndpointAsync(request);
            UpdateData(response.Data);
            GameDebug.Log($"Tutorial step {tutorialId} completed.", "TutorialController");
        }
    }
}

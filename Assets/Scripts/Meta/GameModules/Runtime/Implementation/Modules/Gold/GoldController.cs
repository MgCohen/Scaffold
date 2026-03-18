using System.Linq;
using System.Threading.Tasks;
using GameModuleDTO.Modules.Gold;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class GoldController : GameModule<GoldModuleData>
    {
        protected override async Task OnInitialize(GoldModuleData gameModuleData)
        {
            cloudService.SubscribeToResponse<CompleteTutorialResponse>(OnTutorialCompleted);
            cloudService.SubscribeToResponse<CompleteLevelResponse>(OnLevelCompleted);
            await Task.Yield();
        }

        protected override async Task OnUpdateData(GoldModuleData gameModuleData)
        {
            GameDebug.Log($"Gold updated. New balance: {gameModuleData.Current}", "GoldController");
            await Task.CompletedTask;
        }

        private async Task OnTutorialCompleted(CompleteTutorialResponse response)
        {
            await HandleGoldDelta(response);
        }

        private async Task OnLevelCompleted(CompleteLevelResponse response)
        {
            await HandleGoldDelta(response);
        }

        private async Task HandleGoldDelta(ModuleResponse response)
        {
            GoldResponse goldResponse = response.GetModuleResponse<GoldResponse>();
            if (goldResponse == null)
            {
                return;
            }

            long nextValue = Data.Current + goldResponse.GoldDelta;
            Data.SetCurrent(nextValue);
            await OnUpdateData(Data);
        }
    }
}

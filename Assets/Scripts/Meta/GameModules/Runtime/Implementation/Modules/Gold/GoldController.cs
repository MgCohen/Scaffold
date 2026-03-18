using System.Threading.Tasks;
using GameModuleDTO.Modules.Gold;
using Scaffold.Logging;

namespace Scaffold.GameModules
{
    public class GoldController : GameModule<GoldModuleData>
    {
        protected override async Task OnInitialize(GoldModuleData gameModuleData)
        {
            cloudService.SubscribeToResponse<GoldResponse>(HandleGoldDelta);
            await Task.Yield();
        }

        protected override async Task OnUpdateData(GoldModuleData gameModuleData)
        {
            GameDebug.Log($"Gold updated. New balance: {gameModuleData.Current}", "GoldController");
            await Task.CompletedTask;
        }


        private async Task HandleGoldDelta(GoldResponse goldResponse)
        {
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

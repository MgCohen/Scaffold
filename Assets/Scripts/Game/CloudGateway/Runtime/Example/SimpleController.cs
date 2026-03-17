using System.Threading.Tasks;
using GameModuleDTO.Sample.SimpleModule;
using Scaffold.Infra.CloudGateway;
using Scaffold.Logging;

namespace Scaffold.Game.CloudGateway
{
    public class SimpleController : GameModule<SimpleModuleData>
    {
        protected override async Task OnInitialize(SimpleModuleData gameModuleData)
        {
            await Task.Yield();
        }

        protected override async Task OnUpdateData(SimpleModuleData gameModuleData)
        {
            GameDebug.Log($"Simple module updated", "SimpleController");
            await Task.Yield();
        }
    }
}
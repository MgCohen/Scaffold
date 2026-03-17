using System.Threading.Tasks;
using GameModuleDTO.GameModule;

namespace Scaffold.Infra.CloudGateway
{
    /// <summary>
    /// Defines the baseline behavior for a generic game module in the application.
    /// The main goal is to enforce an initialization and data update structure for isolated systems.
    /// It is used by the GameModulesService to manage module lifecycles in an agnostic way.
    /// </summary>
    public interface IGameModule
    {
        public IGameModuleData DataModule { get; }

        public Task Initialize(GameData gameModules);

        public void UpdateData(IGameModuleData gameModuleData);
    }
}
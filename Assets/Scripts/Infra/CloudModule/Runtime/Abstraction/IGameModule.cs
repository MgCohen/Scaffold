using GameModuleDTO.GameModule;
using UnityEngine;

namespace Scaffold.CloudModules.Shared
{
    public interface IGameModule
    {
        public IGameModuleData DataModule { get; }
        public Awaitable Initialize(GameData gameModules);
        public void UpdateData(IGameModuleData gameModuleData);
    }
}
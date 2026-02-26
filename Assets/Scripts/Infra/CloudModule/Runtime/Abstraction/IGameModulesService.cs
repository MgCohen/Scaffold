using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.CloudModules.Shared
{
    public interface IGameModulesService
    {
        public Awaitable InitializeModules(List<IGameModule> modules);
        public Awaitable FetchModuleData(params string[] fetchModuleKeys);
    }
}
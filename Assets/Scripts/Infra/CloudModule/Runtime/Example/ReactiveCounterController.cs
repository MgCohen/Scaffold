using GameModuleDTO.Sample.ReactiveModule;
using UnityEngine;

namespace Scaffold.CloudModules.Example
{
    public class ReactiveCounterController : GameModuleT<ReactiveModuleData>
    {
        protected override Awaitable OnInitialize(ReactiveModuleData gameModuleData)
        {
            throw new System.NotImplementedException();
        }

        protected override Awaitable OnUpdateData(ReactiveModuleData gameModuleData)
        {
            throw new System.NotImplementedException();
        }
    }
}
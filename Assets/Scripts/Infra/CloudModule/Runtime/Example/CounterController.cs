using GameModuleDTO.Sample.CounterModule;
using UnityEngine;

namespace Scaffold.CloudModules.Example
{
    public class CounterController : GameModuleT<CounterModuleData>
    {
        protected override Awaitable OnInitialize(CounterModuleData gameModuleData)
        {
            throw new System.NotImplementedException();
        }

        protected override Awaitable OnUpdateData(CounterModuleData gameModuleData)
        {
            throw new System.NotImplementedException();
        }
    }
}
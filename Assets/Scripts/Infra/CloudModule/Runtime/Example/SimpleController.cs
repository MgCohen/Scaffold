
using GameModuleDTO.Sample.SimpleModule;
using UnityEngine;

namespace Scaffold.CloudModules.Example
{
    public class SimpleController : GameModuleT<SimpleModuleData>
    {
        protected override Awaitable OnInitialize(SimpleModuleData gameModuleData)
        {
            throw new System.NotImplementedException();
        }

        protected override Awaitable OnUpdateData(SimpleModuleData gameModuleData)
        {
            throw new System.NotImplementedException();
        }
    }
}
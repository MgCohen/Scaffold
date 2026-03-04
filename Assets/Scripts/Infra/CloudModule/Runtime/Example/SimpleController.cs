
using GameModuleDTO.Sample.SimpleModule;
using Scaffold.Logging;
using UnityEngine;

namespace Scaffold.CloudModules.Example
{
    public class SimpleController : GameModuleT<SimpleModuleData>
    {
        protected override async Awaitable OnInitialize(SimpleModuleData gameModuleData)
        {
            await Awaitable.NextFrameAsync();
        }

        protected override async Awaitable OnUpdateData(SimpleModuleData gameModuleData)
        {
            GameDebug.Log($"Simple module updated", "SimpleController");
            await Awaitable.NextFrameAsync();
        }
    }
}
using Scaffold.Containers;
using Scaffold.LifeCycle;
using Scaffold.UGS;
using UnityEngine;

namespace Scaffold.GameModules.Example
{
    public class GameModulesTestBootstrap : Bootstrap
    {
        protected override void Build(IContext context)
        {
            context.AddChild(new GameModulesTestContainer());
        }
    }

    public class GameModulesTestContainer : Container
    {
        public override void Build(IContainerRegistry registry, Transform holder)
        {
            new UGSInstaller().Install(registry, holder);
            new CloudGatewayExampleInstaller().Install(registry, holder);
            new LifeCycleInstaller().Install(registry, holder);
        }
    }
}

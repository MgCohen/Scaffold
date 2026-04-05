using Scaffold.DirectPush;
using Scaffold.LiveOps.Container;
using Scaffold.Scope;
using System;
using VContainer;

namespace Scaffold.Bootstrap.Layers
{
    internal sealed class BootstrapCoreInstaller : LayerInstallerBase
    {
        protected override void Install(IContainerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var liveOpsInstaller = new LiveOpsInstaller();
            BuildInstaller(builder, liveOpsInstaller);

            var directPushInstaller = new DirectPushInstaller();
            BuildInstaller(builder, directPushInstaller);
        }
    }
}


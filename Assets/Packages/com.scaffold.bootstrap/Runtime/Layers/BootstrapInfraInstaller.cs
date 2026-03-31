using System;
using Scaffold.CloudCode.Container;
using Scaffold.SceneFlow;
using Scaffold.SceneFlow.Contracts;
using Scaffold.Scope;
using Scaffold.Ugs.Container;
using Scaffold.Events.Container;
using Scaffold.Navigation;
using Scaffold.Navigation.Container;
using UnityEngine;
using VContainer;

namespace Scaffold.Bootstrap.Layers
{
    internal sealed class BootstrapInfraInstaller : LayerInstallerBase
    {
        internal BootstrapInfraInstaller(Transform viewHolder, ISceneFlowBootstrapShell sceneFlowBootstrapShell)
        {
            this.viewHolder = viewHolder;
            this.sceneFlowBootstrapShell = sceneFlowBootstrapShell;
        }

        private readonly Transform viewHolder;
        private readonly ISceneFlowBootstrapShell sceneFlowBootstrapShell;

        protected override void Install(IContainerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            InstallSharedInfra(builder);
        }

        private void InstallSharedInfra(IContainerBuilder builder)
        {
            EventsInstaller eventsInstaller = new EventsInstaller();
            Install(builder, eventsInstaller);

            NavigationInstaller navigationInstaller = new NavigationInstaller(viewHolder);
            Install(builder, navigationInstaller);

            UgsInstaller ugsInstaller = new UgsInstaller();
            Install(builder, ugsInstaller);

            CloudCodeInstaller cloudCodeInstaller = new CloudCodeInstaller();
            Install(builder, cloudCodeInstaller);

            SceneFlowInstaller sceneFlowInstaller = new SceneFlowInstaller(sceneFlowBootstrapShell);
            Install(builder, sceneFlowInstaller);
        }
    }
}

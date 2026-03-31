using Scaffold.Bootstrap.Layers;
using Scaffold.Scope;
using Scaffold.Scope.Contracts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Bootstrap
{
    public sealed class BootstrapScope : LayeredScope
    {
        [SerializeField] private Transform viewHolder;
        [SerializeField] private BootstrapLoadingView bootstrapLoadingView;
        [SerializeField] private SceneFlowBootstrapShell sceneFlowBootstrapShell;

        private void OnValidate()
        {
            if (sceneFlowBootstrapShell == null)
            {
                sceneFlowBootstrapShell = GetComponent<SceneFlowBootstrapShell>();
            }
        }

        protected override LayerInstallerBase BuildLayerTree()
        {
            if (sceneFlowBootstrapShell == null)
            {
                sceneFlowBootstrapShell = GetComponent<SceneFlowBootstrapShell>();
            }

            Debug.Log("[BootstrapScope] Building Layer Tree");
            BootstrapAssetInstaller asset = new BootstrapAssetInstaller();
            BootstrapInfraInstaller infra = new BootstrapInfraInstaller(viewHolder, sceneFlowBootstrapShell);
            BootstrapCoreInstaller core = new BootstrapCoreInstaller();
            asset.AddChild(infra);
            infra.AddChild(core);
            return asset;
        }

        protected override void OnBootstrapCompleted(LifetimeScope finalScope)
        {
            Debug.Log("Bootstrap completed");
            if (bootstrapLoadingView != null)
            {
                bootstrapLoadingView.Hide();
            }
        }
    }
}

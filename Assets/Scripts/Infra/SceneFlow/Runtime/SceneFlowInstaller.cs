using Scaffold.SceneFlow.Contracts;
using VContainer;
using VContainer.Unity;

namespace Scaffold.SceneFlow
{
    /// <summary>
    /// Registers additive scene flow services. Optional <see cref="ISceneFlowBootstrapShell"/> (e.g. <c>SceneFlowBootstrapShell</c> in the scene) suppresses Bootstrap camera/listener while additive content is active.
    /// </summary>
    public sealed class SceneFlowInstaller : IInstaller
    {
        public SceneFlowInstaller(ISceneFlowBootstrapShell bootstrapShell = null)
        {
            this.bootstrapShell = bootstrapShell;
        }
        
        private readonly ISceneFlowBootstrapShell bootstrapShell;

        public void Install(IContainerBuilder builder)
        {
            if (bootstrapShell != null)
            {
                builder.RegisterInstance(bootstrapShell).As<ISceneFlowBootstrapShell>();
            }

            builder.Register<IAddressablesSceneOperations, AddressablesSceneOperations>(Lifetime.Singleton);

            // Capture shell in a factory so SceneFlowService always receives the scene instance without requiring
            // ISceneFlowBootstrapShell to be resolvable from the container (optional injection is unreliable here).
            ISceneFlowBootstrapShell shellForService = bootstrapShell;
            builder.Register<ISceneFlowService>(
                c =>
                {
                    IAddressablesSceneOperations ops = c.Resolve<IAddressablesSceneOperations>();
                    return new SceneFlowService(ops, shellForService);
                },
                Lifetime.Singleton);
        }
    }
}

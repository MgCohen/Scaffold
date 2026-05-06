#nullable enable

using VContainer;
using VContainer.Unity;

namespace Scaffold.States.Container
{
    // Registers StoreBuilder, Store (singleton), and default IStateEventHandler with VContainer (ExecPlan Phase 6).
    public sealed class StatesInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<StoreBuilder>(Lifetime.Transient);
            builder.Register(container => container.Resolve<StoreBuilder>().Build(), Lifetime.Singleton);
            builder.Register<IStateEventHandler>(_ => StateEventHandlerFactory.CreateDefault(), Lifetime.Singleton);
        }
    }
}

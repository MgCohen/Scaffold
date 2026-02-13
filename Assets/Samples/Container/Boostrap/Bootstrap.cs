using Scaffold.Events;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class Bootstrap : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);
        builder.Register<IEventBus, EventController>(Lifetime.Scoped);
    }
}

public class ControllerScope
{

}

public class InterfaceScope
{

}

using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.Events.Container
{
    public class EventsInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<IEventBus, EventController>(ContainerLifetime.Scoped);
        }
    }
}

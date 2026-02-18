using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.Events.Container
{
    public class EventsInstaller : Installer
    {
        public override void Install(IContainerBuilder builder, Transform holder)
        {
            builder.Register<IEventBus, EventController>(ContainerLifetime.Scoped);
        }
    }
}

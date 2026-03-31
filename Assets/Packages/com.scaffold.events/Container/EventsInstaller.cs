using Scaffold.Events.Contracts;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Events.Container
{
    public class EventsInstaller : IInstaller
    {

        public void Install(IContainerBuilder builder)
        {
            builder.Register<IEventBus, EventController>(Lifetime.Singleton);
        }
    }
}


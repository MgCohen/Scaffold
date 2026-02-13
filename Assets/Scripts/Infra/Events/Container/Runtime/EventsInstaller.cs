using Scaffold.Events;
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

[Serializable]
public class EventsInstaller : IInstaller
{
    public void Install(IContainerBuilder builder)
    {
        builder.Register<IEventBus, EventController>(Lifetime.Scoped);
    }
}

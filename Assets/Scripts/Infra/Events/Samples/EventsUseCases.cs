using System;
using UnityEngine;

namespace Scaffold.Events.Samples
{
    public class EventsUseCases
    {
        public void UseCaseSubscribeAndPublishEvent()
        {
            EventController bus = new EventController();
            bool received = false;
            bus.AddListener<PlayerDiedEvent>(_ => received = true);
            var evt = new PlayerDiedEvent();
            bus.Raise(evt);
            Debug.Log("Received: " + received);
        }

        public void UseCaseUnsubscribeFromEvent()
        {
            EventController bus = new EventController();
            int count = 0;
            Action<PlayerDiedEvent> handler = _ => count++;
            bus.AddListener(handler);
            bus.RemoveListener(handler);
            var evt = new PlayerDiedEvent();
            bus.Raise(evt);
        }

        public void UseCaseSubscribeAndUnsubscribeWithOpenTypeApi()
        {
            EventController bus = new EventController();
            int count = RunOpenTypeLifecycle(bus);
            Debug.Log("OpenTypeCount: " + count);
        }

        private static int RunOpenTypeLifecycle(EventController bus)
        {
            int count = 0;
            Action<ContextEvent> handler = _ => count++;
            Type eventType = typeof(PlayerDiedEvent);
            bus.AddListener(eventType, handler);
            RaisePlayerDiedEvent(bus);
            bus.RemoveListener(eventType, handler);
            RaisePlayerDiedEvent(bus);
            return count;
        }

        private static void RaisePlayerDiedEvent(EventController bus)
        {
            PlayerDiedEvent evt = new PlayerDiedEvent();
            bus.Raise(evt);
        }

        private record PlayerDiedEvent : ContextEvent;
    }
}

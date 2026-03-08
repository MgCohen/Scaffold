using System;

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

        private record PlayerDiedEvent : ContextEvent;
    }
}

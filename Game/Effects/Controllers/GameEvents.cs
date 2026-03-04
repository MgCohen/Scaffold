using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scaffold.Effects
{
    public class GameEvents : IGameEvents
    {
        private Dictionary<Type, List<IEventSubscription>> subscriptions = new();

        public IEventSubscription SubscribeTo<T>(Func<T, Task> callback)
        {
            var subscription = new EventSubscription<T>(callback, this);
            if (!subscriptions.ContainsKey(typeof(T)))
            {
                subscriptions[typeof(T)] = new List<IEventSubscription>();
            }
            subscriptions[typeof(T)].Add(subscription);
            return subscription;
        }

        public List<IEventSubscription> GetSubscriptions(Type type)
        {
            if (!subscriptions.ContainsKey(type))
            {
                return new List<IEventSubscription>();
            }
            return subscriptions[type];
        }

        private void StopListeningTo<T>(IEventSubscription subscription)
        {
            if (!subscriptions.ContainsKey(typeof(T)))
            {
                return;
            }
            subscriptions[typeof(T)].Remove(subscription);
        }

        public async Task Notify(object payload)
        {
            var subscriptions = GetSubscriptions(payload.GetType());
            foreach(var subscription in subscriptions)
            {
                await subscription.Notify(payload);
            }
        }

        public void ClearSubscriptions()
        {
            subscriptions.Clear();
        }

        private class EventSubscription<T> : IEventSubscription
        {
            private Func<T, Task> callback;
            private GameEvents events;

            public EventSubscription(Func<T, Task> callback, GameEvents events)
            {
                this.callback = callback;
                this.events = events;
            }

            public void Unsubscribe()
            {
                events.StopListeningTo<T>(this);
            }

            public async Task Notify(object payload)
            {
                if(payload != null && payload is not T)
                {
                    throw new Exception($"Trying to raise event subscription with the wrong payload. Expected {typeof(T)}, Received {payload.GetType()}");
                }
                await callback((T)payload);
            }
        }
    }
}

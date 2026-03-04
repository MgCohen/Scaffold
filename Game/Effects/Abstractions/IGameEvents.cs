using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.Effects
{
    public interface IGameEvents
    {
        List<IEventSubscription> GetSubscriptions(Type type);
        IEventSubscription SubscribeTo<T>(Func<T, Task> callback);
        Task Notify(object payload);
        void ClearSubscriptions();
    }

    public interface IEventSubscription
    {
        public Task Notify(object effect);
        public void Unsubscribe();
    }
}

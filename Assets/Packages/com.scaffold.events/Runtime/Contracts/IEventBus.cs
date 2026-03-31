using System;

namespace Scaffold.Events.Contracts
{
    public interface IEventBus
    {
        void AddListener<T>(Action<T> evt) where T : ContextEvent;
        void RemoveListener<T>(Action<T> evt) where T : ContextEvent;
        void AddListener(Type type, Action<ContextEvent> evt);
        void RemoveListener(Type type, Action<ContextEvent> evt);
        void Raise(ContextEvent evt);
        void Clear();
    }
}




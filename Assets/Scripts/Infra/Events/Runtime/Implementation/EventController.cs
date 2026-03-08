using System.Collections.Generic;
using System;
using UnityEngine;

namespace Scaffold.Events
{
    public class EventController : IEventBus
    {
        readonly Dictionary<Type, Action<ContextEvent>> events = new Dictionary<Type, Action<ContextEvent>>();
        readonly Dictionary<Delegate, Action<ContextEvent>> eventLookups = new Dictionary<Delegate, Action<ContextEvent>>();

        public void AddListener<T>(Action<T> evt) where T : ContextEvent
        {
            if (eventLookups.ContainsKey(evt))
            {
                return;
            }

            Action<ContextEvent> newAction = (e) => evt((T)e);
            eventLookups[evt] = newAction;
            AddListener(typeof(T), newAction);
        }

        public void AddListener(Type type, Action<ContextEvent> newAction)
        {
            if (events.TryGetValue(type, out Action<ContextEvent> internalAction))
            {
                events[type] = internalAction += newAction;
            }
            else
            {
                events[type] = newAction;
            }
        }

        public void RemoveListener<T>(Action<T> evt) where T : ContextEvent
        {
            if (!eventLookups.TryGetValue(evt, out var action))
            {
                return;
            }

            RemoveListener(typeof(T), action);
            eventLookups.Remove(evt);
        }

        public void RemoveListener(Type type, Action<ContextEvent> action)
        {
            if (!events.TryGetValue(type, out var tempAction))
            {
                return;
            }

            UpdateOrRemoveAction(type, tempAction, action);
        }

        private void UpdateOrRemoveAction(Type type, Action<ContextEvent> tempAction, Action<ContextEvent> action)
        {
            tempAction -= action;
            if (tempAction == null)
            {
                events.Remove(type);
            }
            else
            {
                events[type] = tempAction;
            }
        }

        public void Raise(ContextEvent evt)
        {
            var evtType = evt.GetType();
            if (events.TryGetValue(evtType, out var action))
            {
                action.Invoke(evt);
            }
        }

        public void Clear()
        {
            events.Clear();
            eventLookups.Clear();
        }
    }
}

using System.Collections.Generic;
using System;
using UnityEngine;

namespace Scaffold.Events
{
    public class EventController : MonoBehaviour, IEventBus
    {
        readonly Dictionary<Type, Action<ContextEvent>> m_Events = new Dictionary<Type, Action<ContextEvent>>();
        readonly Dictionary<Delegate, Action<ContextEvent>> m_EventLookups = new Dictionary<Delegate, Action<ContextEvent>>();

        public void AddListener<T>(Action<T> evt) where T : ContextEvent
        {
            if (m_EventLookups.ContainsKey(evt))
            {
                return;
            }

            Action<ContextEvent> newAction = (e) => evt((T)e);
            m_EventLookups[evt] = newAction;

            AddListener(typeof(T), newAction);
        }

        public void AddListener(Type type, Action<ContextEvent> newAction){

            if (m_Events.TryGetValue(type, out Action<ContextEvent> internalAction))
            {
                m_Events[type] = internalAction += newAction;
            }
            else
            {
                m_Events[type] = newAction;
            }
        }

        public void RemoveListener<T>(Action<T> evt) where T : ContextEvent
        {
            if (!m_EventLookups.TryGetValue(evt, out var action))
            {
                return;
            }

            RemoveListener(typeof(T), action);

            m_EventLookups.Remove(evt);
        }

        public void RemoveListener(Type type, Action<ContextEvent> action){

            if (m_Events.TryGetValue(type, out var tempAction))
            {
                tempAction -= action;
                if (tempAction == null)
                {
                    m_Events.Remove(type);
                }
                else
                {
                    m_Events[type] = tempAction;
                }
            }
        }

        public void Raise(ContextEvent evt)
        {
            if (m_Events.TryGetValue(evt.GetType(), out var action))
            {
                action.Invoke(evt);
            }
        }

        public void Clear()
        {
            m_Events.Clear();
            m_EventLookups.Clear();
        }
    }
}

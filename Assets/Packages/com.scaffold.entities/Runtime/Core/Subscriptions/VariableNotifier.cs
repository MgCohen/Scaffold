using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    internal sealed class VariableNotifier
    {
        internal VariableNotifier()
        {
            subscribers = new Dictionary<Variable, Action<VariableValue>>();
        }

        private readonly Dictionary<Variable, Action<VariableValue>> subscribers;

        internal void Add(Variable key, Action<VariableValue> onChange)
        {
            if (subscribers.TryGetValue(key, out Action<VariableValue> existing))
            {
                subscribers[key] = existing + onChange;
            }
            else
            {
                subscribers[key] = onChange;
            }
        }

        internal void Remove(Variable key, Action<VariableValue> onChange)
        {
            if (!subscribers.TryGetValue(key, out Action<VariableValue> existing))
            {
                return;
            }

            Action<VariableValue> updated = existing - onChange;
            if (updated == null)
            {
                subscribers.Remove(key);
            }
            else
            {
                subscribers[key] = updated;
            }
        }

        internal void Notify(Variable key, VariableValue value)
        {
            if (subscribers.TryGetValue(key, out Action<VariableValue> cb))
            {
                cb?.Invoke(value);
            }
        }

        internal void ClearKey(Variable key)
        {
            subscribers.Remove(key);
        }
    }
}

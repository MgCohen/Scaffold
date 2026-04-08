using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    internal sealed class AttributeNotifier
    {
        internal AttributeNotifier()
        {
            subscribers = new Dictionary<Attribute, Action<AttributeValue>>();
        }

        private readonly Dictionary<Attribute, Action<AttributeValue>> subscribers;

        internal void Add(Attribute attribute, Action<AttributeValue> onChange)
        {
            if (subscribers.TryGetValue(attribute, out Action<AttributeValue> existing))
            {
                subscribers[attribute] = existing + onChange;
            }
            else
            {
                subscribers[attribute] = onChange;
            }
        }

        internal void Remove(Attribute attribute, Action<AttributeValue> onChange)
        {
            if (!subscribers.TryGetValue(attribute, out Action<AttributeValue> existing))
            {
                return;
            }

            Action<AttributeValue> updated = existing - onChange;
            if (updated == null)
            {
                subscribers.Remove(attribute);
            }
            else
            {
                subscribers[attribute] = updated;
            }
        }

        internal void Notify(Attribute attribute, AttributeValue value)
        {
            if (subscribers.TryGetValue(attribute, out Action<AttributeValue> cb))
            {
                cb?.Invoke(value);
            }
        }
    }
}

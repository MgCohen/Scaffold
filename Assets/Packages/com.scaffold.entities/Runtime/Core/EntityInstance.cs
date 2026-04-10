using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class EntityInstance<TDefinition> : IEntity<TDefinition> where TDefinition : EntityDefinition
    {
        public InstanceId Id => id;
        [SerializeField] private InstanceId id;

        public TDefinition Definition => definition;
        [SerializeField] private TDefinition definition = default!;

        private AttributeModifierHandler modifierHandler;
        private AttributeNotifier notifier;
        private Dictionary<Attribute, AttributeValue> cache;
        private EmptySubscriptionToken emptySubscription;

        public void Initialize(InstanceId instanceId, TDefinition entityDefinition)
        {
            id = instanceId;
            definition = entityDefinition ?? throw new ArgumentNullException(nameof(entityDefinition));
            entityDefinition.RebuildLookup();
            modifierHandler = new AttributeModifierHandler();
            notifier = new AttributeNotifier();
            emptySubscription = new EmptySubscriptionToken();
            SeedCache();
        }

        public T GetValue<T>(Attribute attribute)
        {
            if (!cache.TryGetValue(attribute, out AttributeValue av) || av == null)
            {
                throw new InvalidOperationException(
                    $"Attribute '{attribute?.Key ?? "?"}' is not defined on this entity.");
            }

            if (av is IAttributeValue<T> typed)
            {
                return typed.Get();
            }

            throw new InvalidCastException(
                $"Attribute '{attribute?.Key ?? "?"}' has type {av.Type} but {typeof(T).Name} was requested.");
        }

        public TAttr GetAttribute<TAttr>(Attribute attribute) where TAttr : AttributeValue
        {
            if (!cache.TryGetValue(attribute, out AttributeValue av) || av == null)
            {
                throw new InvalidOperationException(
                    $"Attribute '{attribute?.Key ?? "?"}' is not defined on this entity.");
            }

            if (av is TAttr typed)
            {
                return typed;
            }

            throw new InvalidCastException(
                $"Attribute '{attribute?.Key ?? "?"}' is {av.GetType().Name} but {typeof(TAttr).Name} was requested.");
        }

        public bool TryGetAttribute<T>(Attribute attribute, out T value) where T : AttributeValue
        {
            value = default!;
            if (!cache.TryGetValue(attribute, out AttributeValue av) || av == null)
            {
                return false;
            }

            if (av is T typed)
            {
                value = typed;
                return true;
            }

            return false;
        }

        public void AddModifier(EntityModifierEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            modifierHandler.AddModifier(entry);
            RecalculateAndNotify(entry.AttributeKey);
        }

        public bool RemoveModifier(EntityModifierEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            bool removed = modifierHandler.RemoveModifier(entry);
            if (removed)
            {
                RecalculateAndNotify(entry.AttributeKey);
            }

            return removed;
        }

        public void ClearModifiers()
        {
            var affectedKeys = new List<Attribute>(modifierHandler.ModifiedAttributes);
            modifierHandler.ClearModifiers();
            for (int i = 0; i < affectedKeys.Count; i++)
            {
                RecalculateAndNotify(affectedKeys[i]);
            }
        }

        public IDisposable Subscribe(Attribute attribute, Action<AttributeValue> onChange)
        {
            if (attribute == null || onChange == null)
            {
                return emptySubscription;
            }

            return RegisterSubscription(attribute, onChange);
        }

        public IDisposable Subscribe<T>(Attribute attribute, Action<T> onChange)
        {
            if (attribute == null || onChange == null)
            {
                return emptySubscription;
            }

            Action<AttributeValue> adapter = CreateRawValueAdapter(onChange);
            return RegisterSubscription(attribute, adapter);
        }

        public IDisposable SubscribeToAttribute<TAttr>(Attribute attribute, Action<TAttr> onChange) where TAttr : AttributeValue
        {
            if (attribute == null || onChange == null)
            {
                return emptySubscription;
            }

            Action<AttributeValue> adapter = CreateAttributeValueAdapter(onChange);
            return RegisterSubscription(attribute, adapter);
        }

        public void Unsubscribe(Attribute attribute, Action<AttributeValue> onChange)
        {
            if (attribute == null || onChange == null)
            {
                return;
            }

            notifier.Remove(attribute, onChange);
        }

        private IDisposable RegisterSubscription(Attribute attribute, Action<AttributeValue> adapter)
        {
            notifier.Add(attribute, adapter);

            if (cache.TryGetValue(attribute, out AttributeValue current))
            {
                adapter(current);
            }

            return new AttributeSubscriptionToken(notifier, attribute, adapter);
        }

        private Action<AttributeValue> CreateRawValueAdapter<T>(Action<T> onChange)
        {
            return av =>
            {
                if (av is IAttributeValue<T> typed)
                {
                    onChange(typed.Get());
                }
            };
        }

        private Action<AttributeValue> CreateAttributeValueAdapter<TAttr>(Action<TAttr> onChange) where TAttr : AttributeValue
        {
            return av =>
            {
                if (av is TAttr typed)
                {
                    onChange(typed);
                }
            };
        }

        private void SeedCache()
        {
            cache = new Dictionary<Attribute, AttributeValue>();
            IReadOnlyList<AttributeEntry> entries = definition.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                AttributeEntry entry = entries[i];
                if (entry?.Attribute == null || entry.BaseValue == null)
                {
                    continue;
                }

                cache[(Attribute)entry.Attribute] = entry.BaseValue;
            }
        }

        private void RecalculateAndNotify(Attribute key)
        {
            if (!definition.TryGetBaseValue(key, out AttributeValue baseValue))
            {
                return;
            }

            AttributeValue newValue = modifierHandler.GetEffective(key, baseValue);
            cache[key] = newValue;
            notifier.Notify(key, newValue);
        }

        private sealed class EmptySubscriptionToken : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}

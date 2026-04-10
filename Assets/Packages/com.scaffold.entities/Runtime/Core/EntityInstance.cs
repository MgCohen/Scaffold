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

        [SerializeField] private AttributeBag instanceBaseBag = new AttributeBag();
        [SerializeField] private AttributeBag instanceEffectiveBag = new AttributeBag();

        private AttributeModifierHandler modifierHandler = null!;
        private AttributeNotifier notifier = null!;

        internal bool ContainsModifiedValueCache(Attribute key)
        {
            return instanceEffectiveBag != null && instanceEffectiveBag.HasLocalKey(key);
        }

        internal bool InstanceBagHasLocalKey(Attribute key)
        {
            return instanceBaseBag != null && instanceBaseBag.HasLocalKey(key);
        }

        public void Initialize(InstanceId instanceId, TDefinition entityDefinition)
        {
            id = instanceId;
            definition = entityDefinition ?? throw new ArgumentNullException(nameof(entityDefinition));
            entityDefinition.RebuildLookup();
            modifierHandler = new AttributeModifierHandler();
            notifier = new AttributeNotifier();
            EnsureInstanceBags();
            WireBagParentsToDefinition(entityDefinition);
        }

        public T GetValue<T>(Attribute attribute)
        {
            if (!TryResolve(attribute, out AttributeValue av) || av == null)
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
            if (!TryResolve(attribute, out AttributeValue av) || av == null)
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
            if (!TryResolve(attribute, out AttributeValue av) || av == null)
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
                return EmptyDisposable.Instance;
            }

            return RegisterSubscription(attribute, onChange);
        }

        public IDisposable Subscribe<T>(Attribute attribute, Action<T> onChange)
        {
            if (attribute == null || onChange == null)
            {
                return EmptyDisposable.Instance;
            }

            Action<AttributeValue> adapter = CreateRawValueAdapter(onChange);
            return RegisterSubscription(attribute, adapter);
        }

        public IDisposable SubscribeToAttribute<TAttr>(Attribute attribute, Action<TAttr> onChange) where TAttr : AttributeValue
        {
            if (attribute == null || onChange == null)
            {
                return EmptyDisposable.Instance;
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

        public bool AddRuntimeAttribute(Attribute key, AttributeValue initialBase)
        {
            if (!instanceBaseBag.Add(key, initialBase))
            {
                return false;
            }

            RecalculateAndNotify(key);
            return true;
        }

        public bool RemoveRuntimeAttribute(Attribute key)
        {
            if (!instanceBaseBag.Remove(key))
            {
                return false;
            }

            instanceEffectiveBag.RemoveLocalSilent(key);
            modifierHandler.ClearModifiersForKey(key);
            notifier.ClearKey(key);
            return true;
        }

        public IDisposable SubscribeToAttributeAdded(Action<Attribute, AttributeValue> onAdded)
        {
            if (onAdded == null)
            {
                return EmptyDisposable.Instance;
            }

            void Handler(Attribute key, AttributeValue value) => onAdded(key, value);
            instanceBaseBag.OnAttributeAdded += Handler;
            return new CallbackDisposable(() => instanceBaseBag.OnAttributeAdded -= Handler);
        }

        public IDisposable SubscribeToAttributeRemoved(Action<Attribute> onRemoved)
        {
            if (onRemoved == null)
            {
                return EmptyDisposable.Instance;
            }

            void Handler(Attribute key) => onRemoved(key);
            instanceBaseBag.OnAttributeRemoved += Handler;
            return new CallbackDisposable(() => instanceBaseBag.OnAttributeRemoved -= Handler);
        }

        private IDisposable RegisterSubscription(Attribute attribute, Action<AttributeValue> adapter)
        {
            notifier.Add(attribute, adapter);

            if (TryResolve(attribute, out AttributeValue current))
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

        private void EnsureInstanceBags()
        {
            if (instanceBaseBag == null)
            {
                instanceBaseBag = new AttributeBag();
            }

            if (instanceEffectiveBag == null)
            {
                instanceEffectiveBag = new AttributeBag();
            }
        }

        private void WireBagParentsToDefinition(TDefinition entityDefinition)
        {
            instanceBaseBag.SetParent(entityDefinition.Bag);
            instanceBaseBag.RebuildCache();
            instanceEffectiveBag.SetParent(instanceBaseBag);
            instanceEffectiveBag.RebuildCache();
        }

        private bool TryResolve(Attribute key, out AttributeValue value)
        {
            return instanceEffectiveBag.TryGetBase(key, out value);
        }

        private void RecalculateAndNotify(Attribute key)
        {
            if (!instanceBaseBag.TryGetBase(key, out AttributeValue baseValue))
            {
                return;
            }

            AttributeValue effective = modifierHandler.GetEffective(key, baseValue);

            if (modifierHandler.HasModifiersFor(key))
            {
                instanceEffectiveBag.SetLocalSilent(key, effective);
            }
            else
            {
                instanceEffectiveBag.RemoveLocalSilent(key);
            }

            notifier.Notify(key, effective);
        }
    }
}

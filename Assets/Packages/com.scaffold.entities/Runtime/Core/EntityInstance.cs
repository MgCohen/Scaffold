using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public partial class EntityInstance<TDefinition> : IInstance<TDefinition> where TDefinition : IEntityDefinition
    {
        public InstanceId Id => id;
        [SerializeField] private InstanceId id;

        internal TDefinition Definition => definition;
        [SerializeField] protected TDefinition definition = default!;

        [SerializeField] private VariableBag instanceBaseBag = new VariableBag();
        [SerializeField] private VariableBag instanceEffectiveBag = new VariableBag();

        private VariableModifierHandler modifierHandler = null!;
        private VariableNotifier notifier = null!;

        public void Initialize(InstanceId instanceId, TDefinition entityDefinition)
        {
            id = instanceId;
            definition = entityDefinition ?? throw new ArgumentNullException(nameof(entityDefinition));
            if (entityDefinition is IDefinitionVariableBagProvider bagSource)
            {
                bagSource.RebuildLookup();
            }
            modifierHandler = new VariableModifierHandler();
            notifier = new VariableNotifier();
            EnsureInstanceBags();
            WireBagParentsToDefinition(entityDefinition);
        }

        public T GetValue<T>(Variable key)
        {
            if (!TryResolve(key, out VariableValue av) || av == null)
            {
                throw new InvalidOperationException(
                    $"Variable '{key?.Key ?? "?"}' is not defined on this entity.");
            }

            if (av is IVariableValue<T> typed)
            {
                return typed.Get();
            }

            throw new InvalidCastException(
                $"Variable '{key?.Key ?? "?"}' has type {av.Type} but {typeof(T).Name} was requested.");
        }

        public bool TryGetValue<T>(Variable key, out T value)
        {
            value = default!;
            if (!TryResolve(key, out VariableValue av) || av == null)
            {
                return false;
            }

            if (av is IVariableValue<T> typed)
            {
                value = typed.Get();
                return true;
            }

            return false;
        }

        public TVar GetVariable<TVar>(Variable key) where TVar : VariableValue
        {
            if (!TryResolve(key, out VariableValue av) || av == null)
            {
                throw new InvalidOperationException(
                    $"Variable '{key?.Key ?? "?"}' is not defined on this entity.");
            }

            if (av is TVar typed)
            {
                return typed;
            }

            throw new InvalidCastException(
                $"Variable '{key?.Key ?? "?"}' is {av.GetType().Name} but {typeof(TVar).Name} was requested.");
        }

        public bool TryGetVariable<TVar>(Variable key, out TVar value) where TVar : VariableValue
        {
            value = default!;
            if (!TryResolve(key, out VariableValue av) || av == null)
            {
                return false;
            }

            if (av is TVar typed)
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
            RecalculateAndNotify(entry.Key);
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
                RecalculateAndNotify(entry.Key);
            }

            return removed;
        }

        public void ClearModifiers()
        {
            var affectedKeys = new List<Variable>(modifierHandler.ModifiedVariables);
            modifierHandler.ClearModifiers();
            for (int i = 0; i < affectedKeys.Count; i++)
            {
                RecalculateAndNotify(affectedKeys[i]);
            }
        }

        public IDisposable Subscribe(Variable key, Action<VariableValue> onChange)
        {
            if (key == null || onChange == null)
            {
                return EmptyDisposable.Instance;
            }

            return RegisterSubscription(key, onChange);
        }

        public void Unsubscribe(Variable key, Action<VariableValue> onChange)
        {
            if (key == null || onChange == null)
            {
                return;
            }

            notifier.Remove(key, onChange);
        }

        public bool AddVariable(Variable key, VariableValue initialBase)
        {
            if (!instanceBaseBag.Add(key, initialBase))
            {
                return false;
            }

            RecalculateAndNotify(key);
            return true;
        }

        public bool RemoveVariable(Variable key)
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

        public IDisposable SubscribeToVariableAdded(Action<Variable, VariableValue> onAdded)
        {
            if (onAdded == null)
            {
                return EmptyDisposable.Instance;
            }

            void Handler(Variable k, VariableValue value) => onAdded(k, value);
            instanceBaseBag.OnVariableAdded += Handler;
            return new CallbackDisposable(() => instanceBaseBag.OnVariableAdded -= Handler);
        }

        public IDisposable SubscribeToVariableRemoved(Action<Variable> onRemoved)
        {
            if (onRemoved == null)
            {
                return EmptyDisposable.Instance;
            }

            void Handler(Variable k) => onRemoved(k);
            instanceBaseBag.OnVariableRemoved += Handler;
            return new CallbackDisposable(() => instanceBaseBag.OnVariableRemoved -= Handler);
        }

        internal bool ContainsModifiedValueCache(Variable key)
        {
            return instanceEffectiveBag != null && instanceEffectiveBag.HasLocalKey(key);
        }

        internal bool InstanceBagHasLocalKey(Variable key)
        {
            return instanceBaseBag != null && instanceBaseBag.HasLocalKey(key);
        }

        internal bool TryResolveKeyByName(string name, out Variable key)
        {
            if (TryFindKeyInDefinition(name, out key))
            {
                return true;
            }

            if (TryFindKeyInBagLocalKeys(instanceBaseBag, name, out key))
            {
                return true;
            }

            return TryFindKeyInBagLocalKeys(instanceEffectiveBag, name, out key);
        }

        private bool TryFindKeyInDefinition(string name, out Variable key)
        {
            key = default!;
            if (definition == null)
            {
                return false;
            }

            foreach (Variable v in definition.DefinedVariables)
            {
                if (v.Key == name)
                {
                    key = v;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindKeyInBagLocalKeys(VariableBag bag, string name, out Variable key)
        {
            foreach (Variable k in bag.LocalKeys)
            {
                if (k.Key == name)
                {
                    key = k;
                    return true;
                }
            }

            key = default!;
            return false;
        }

        private IDisposable RegisterSubscription(Variable key, Action<VariableValue> adapter)
        {
            notifier.Add(key, adapter);

            if (TryResolve(key, out VariableValue current))
            {
                adapter(current);
            }

            return new VariableSubscriptionToken(notifier, key, adapter);
        }

        private void EnsureInstanceBags()
        {
            if (instanceBaseBag == null)
            {
                instanceBaseBag = new VariableBag();
            }

            if (instanceEffectiveBag == null)
            {
                instanceEffectiveBag = new VariableBag();
            }
        }



        private void WireBagParentsToDefinition(TDefinition entityDefinition)
        {
            instanceBaseBag.SetParent(
                entityDefinition is IDefinitionVariableBagProvider bagProvider
                    ? bagProvider.Bag
                    : null);

            instanceBaseBag.RebuildCache();
            instanceEffectiveBag.SetParent(instanceBaseBag);
            instanceEffectiveBag.RebuildCache();
        }

        private bool TryResolve(Variable key, out VariableValue value)
        {
            return instanceEffectiveBag.TryGetBase(key, out value);
        }

        private void RecalculateAndNotify(Variable key)
        {
            if (!instanceBaseBag.TryGetBase(key, out VariableValue baseValue))
            {
                return;
            }

            VariableValue effective = modifierHandler.GetEffective(key, baseValue);

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

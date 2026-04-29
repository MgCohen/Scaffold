#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed partial class LocalVariableStorage : IEntityVariableStorage
    {
        internal VariableBag InstanceBaseBag => instanceBaseBag;

        [SerializeField] private VariableBag instanceBaseBag = new VariableBag();

        internal VariableBag InstanceEffectiveBag => instanceEffectiveBag;

        [SerializeField] private VariableBag instanceEffectiveBag = new VariableBag();

        public IEnumerable<Variable> Variables
        {
            get
            {
                var seen = new HashSet<Variable>();
                if (wiredDefinition != null)
                {
                    foreach (Variable v in wiredDefinition.DefinedVariables)
                    {
                        if (seen.Add(v))
                        {
                            yield return v;
                        }
                    }
                }

                foreach (Variable v in instanceBaseBag.LocalKeys)
                {
                    if (seen.Add(v))
                    {
                        yield return v;
                    }
                }

                foreach (Variable v in modifierHandler.ModifiedVariables)
                {
                    if (seen.Add(v))
                    {
                        yield return v;
                    }
                }
            }
        }

        [NonSerialized] private VariableModifierHandler modifierHandler = new();
        [NonSerialized] private VariableNotifier notifier = new();
        [NonSerialized] private IEntityDefinition wiredDefinition = default!;

        internal void WireToDefinition(IEntityDefinition entityDefinition)
        {
            wiredDefinition = entityDefinition ?? throw new ArgumentNullException(nameof(entityDefinition));
            instanceBaseBag.SetParent(
                entityDefinition is IDefinitionVariableBagProvider bagProvider
                    ? bagProvider.Bag
                    : null);

            instanceBaseBag.RebuildCache();
            instanceEffectiveBag.SetParent(instanceBaseBag);
            instanceEffectiveBag.RebuildCache();
        }

        public IDisposable Subscribe(Variable key, Action<VariableValue> onChange)
        {
            if (key == null || onChange == null)
            {
                return EmptyDisposable.Instance;
            }

            notifier.Add(key, onChange);

            if (instanceEffectiveBag.TryGetBase(key, out VariableValue current))
            {
                onChange(current);
            }

            return new CallbackDisposable(() => notifier.Remove(key, onChange));
        }

        public void Unsubscribe(Variable key, Action<VariableValue> onChange)
        {
            if (key == null || onChange == null)
            {
                return;
            }

            notifier.Remove(key, onChange);
        }

        public IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler)
        {
            if (handler == null)
            {
                return EmptyDisposable.Instance;
            }

            void Structural(VariableStructuralChange kindArg, Variable keyArg, VariableValue? value)
            {
                handler(kindArg, keyArg, value);
            }

            instanceBaseBag.OnVariableStructuralChange += Structural;
            return new CallbackDisposable(() => instanceBaseBag.OnVariableStructuralChange -= Structural);
        }

        public bool TryGetEffective(Variable key, out VariableValue value)
        {
            return instanceEffectiveBag.TryGetBase(key, out value);
        }

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            return instanceBaseBag.TryGetBase(key, out value);
        }

        internal bool ContainsModifiedValueCache(Variable key)
        {
            return instanceEffectiveBag != null && instanceEffectiveBag.HasLocalKey(key);
        }

        internal bool InstanceBagHasLocalKey(Variable key)
        {
            return instanceBaseBag != null && instanceBaseBag.HasLocalKey(key);
        }

        internal ModifierId AddModifier(EntityModifierEntry entry)
        {
            if (entry == null)
            {
                return default;
            }

            ModifierId id = modifierHandler.AddModifier(entry);
            if (id.Id == default)
            {
                return default;
            }

            RecalculateAndNotify(entry.Key);
            return id;
        }

        internal bool RemoveModifier(Variable key, ModifierId id)
        {
            bool removed = modifierHandler.RemoveModifier(key, id);
            if (removed && key != null)
            {
                RecalculateAndNotify(key);
            }

            return removed;
        }

        internal void ClearModifiers()
        {
            List<Variable> affectedKeys = new List<Variable>(modifierHandler.ModifiedVariables);
            modifierHandler.ClearModifiers();
            for (int i = 0; i < affectedKeys.Count; i++)
            {
                RecalculateAndNotify(affectedKeys[i]);
            }
        }

        internal bool AddVariable(Variable key, VariableValue initialBase)
        {
            if (!instanceBaseBag.Add(key, initialBase))
            {
                return false;
            }

            RecalculateAndNotify(key);
            return true;
        }

        internal bool RemoveVariable(Variable key)
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

        internal void RecalculateAndNotify(Variable key)
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

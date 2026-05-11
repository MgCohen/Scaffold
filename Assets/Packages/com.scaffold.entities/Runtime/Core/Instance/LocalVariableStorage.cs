#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed partial class LocalVariableStorage : IEntityVariableStorage
    {
        public IEntityVariableStorage? Parent { get; }

        [SerializeField] private VariableBag instanceBaseBag = new VariableBag();

        [NonSerialized] private Dictionary<Variable, List<ActiveModifier>> modifiers = new();

        public LocalVariableStorage() { }

        public LocalVariableStorage(IEntityVariableStorage? parent)
        {
            Parent = parent;
        }

        internal VariableBag InstanceBaseBag => instanceBaseBag;

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            if (instanceBaseBag.TryGetBase(key, out value))
            {
                return true;
            }

            if (Parent != null)
            {
                return Parent.TryGetBase(key, out value);
            }

            value = default!;
            return false;
        }

        public IEnumerable<ActiveModifier> GetModifiers(Variable key)
        {
            IEnumerable<ActiveModifier> local = modifiers.TryGetValue(key, out var list)
                ? list
                : Array.Empty<ActiveModifier>();
            IEnumerable<ActiveModifier> parentMods = Parent != null
                ? Parent.GetModifiers(key)
                : Array.Empty<ActiveModifier>();
            return local.Concat(parentMods).OrderBy(m => m.Modifier.Order);
        }

        public IEnumerable<Variable> Variables
        {
            get
            {
                var seen = new HashSet<Variable>();
                foreach (Variable v in instanceBaseBag.LocalKeys)
                {
                    if (seen.Add(v))
                    {
                        yield return v;
                    }
                }

                foreach (Variable v in modifiers.Keys)
                {
                    if (seen.Add(v))
                    {
                        yield return v;
                    }
                }

                if (Parent != null)
                {
                    foreach (Variable v in Parent.Variables)
                    {
                        if (seen.Add(v))
                        {
                            yield return v;
                        }
                    }
                }
            }
        }

        public bool AddVariable(Variable key, VariableValue initial)
        {
            return instanceBaseBag.Add(key, initial);
        }

        public bool RemoveVariable(Variable key)
        {
            if (!instanceBaseBag.Remove(key))
            {
                return false;
            }

            modifiers.Remove(key);
            return true;
        }

        public bool SetBaseValue(Variable key, VariableValue value)
        {
            if (instanceBaseBag.HasLocalKey(key))
            {
                instanceBaseBag.SetLocalSilent(key, value);
                return true;
            }

            return instanceBaseBag.Add(key, value);
        }

        public ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null)
        {
            ModifierId resolvedId = id ?? ModifierId.New();
            var active = new ActiveModifier(resolvedId, mod, source);
            if (!modifiers.TryGetValue(key, out var list))
            {
                list = new List<ActiveModifier>();
                modifiers[key] = list;
            }

            list.Add(active);
            return resolvedId;
        }

        public bool RemoveModifier(Variable key, ModifierId id)
        {
            if (!modifiers.TryGetValue(key, out var list))
            {
                return false;
            }

            return list.RemoveAll(m => m.Id == id) > 0;
        }

        public void ClearModifiers()
        {
            modifiers.Clear();
        }

        public void RemoveModifiersFromSource(ModifierSource source)
        {
            foreach (var list in modifiers.Values)
            {
                list.RemoveAll(m => m.Source.HasValue && m.Source.Value.Equals(source));
            }
        }
    }
}

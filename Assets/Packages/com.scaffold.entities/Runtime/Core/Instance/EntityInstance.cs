#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities
{
    public partial class EntityInstance<TDefinition> : IDisposable where TDefinition : IEntityDefinition
    {
        public TDefinition Definition { get; }
        public IEntityVariableStorage Storage { get; }

        public EntityInstance(TDefinition definition, IEntityVariableStorage storage)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public bool TryGetVariable<T>(Variable key, out T value)
        {
            bool hasAnchor = Storage.TryGetBase(key, out var anchor)
                || Definition.TryGetDefaultValue(key, out anchor);

            if (!hasAnchor || anchor == null)
            {
                value = default!;
                return false;
            }

            var mods = Storage.GetModifiers(key).ToList();
            VariableValue folded = mods.Count > 0 ? anchor.ApplyModifiers(mods) : anchor;

            if (folded is IVariableValue<T> typed)
            {
                value = typed.Get();
                return true;
            }

            value = default!;
            return false;
        }

        public T GetVariable<T>(Variable key)
        {
            if (!TryGetVariable<T>(key, out var v))
            {
                throw new KeyNotFoundException(key?.Id ?? "?");
            }
            return v;
        }

        public IEnumerable<Variable> Variables => Storage.Variables.Union(Definition.DefinedVariables);

        public bool AddVariable(Variable key, VariableValue initial) => Storage.AddVariable(key, initial);
        public bool RemoveVariable(Variable key) => Storage.RemoveVariable(key);
        public bool SetBaseValue(Variable key, VariableValue value) => Storage.SetBaseValue(key, value);

        public ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null)
            => Storage.AddModifier(key, mod, source, id);

        public bool RemoveModifier(Variable key, ModifierId id) => Storage.RemoveModifier(key, id);
        public void ClearModifiers() => Storage.ClearModifiers();
        public void RemoveModifiersFromSource(ModifierSource source) => Storage.RemoveModifiersFromSource(source);

        public virtual void Dispose() { }
    }
}

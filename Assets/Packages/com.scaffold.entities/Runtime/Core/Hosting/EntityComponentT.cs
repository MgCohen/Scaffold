#nullable enable
using UnityEngine;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities
{
    public partial class EntityComponent<TDefinition> : EntityComponent where TDefinition : IEntityDefinition
    {
        [SerializeField] private TDefinition definition = default!;

        private EntityInstance<TDefinition>? instance;

        public TDefinition Definition => definition;

        public EntityInstance<TDefinition> Instance
            => instance ??= new EntityInstance<TDefinition>(definition, new LocalVariableStorage());

        public T GetVariable<T>(Variable key) => Instance.GetVariable<T>(key);
        public bool TryGetVariable<T>(Variable key, out T value) => Instance.TryGetVariable(key, out value);
        public bool AddVariable(Variable key, VariableValue initial) => Instance.AddVariable(key, initial);
        public bool RemoveVariable(Variable key) => Instance.RemoveVariable(key);
        public bool SetBaseValue(Variable key, VariableValue value) => Instance.SetBaseValue(key, value);
        public ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null)
            => Instance.AddModifier(key, mod, source, id);
        public bool RemoveModifier(Variable key, ModifierId id) => Instance.RemoveModifier(key, id);
        public void ClearModifiers() => Instance.ClearModifiers();
        public void RemoveModifiersFromSource(ModifierSource source) => Instance.RemoveModifiersFromSource(source);
    }
}

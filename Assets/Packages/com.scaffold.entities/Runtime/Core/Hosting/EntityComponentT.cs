using System;
using UnityEngine;

namespace Scaffold.Entities
{
    public partial class EntityComponent<TDefinition> : EntityComponent, IMutableEntity<TDefinition> where TDefinition : IEntityDefinition
    {
        internal EntityInstance<TDefinition> Instance => instance;
        [SerializeField] private EntityInstance<TDefinition> instance;

        public InstanceId Id => Instance.Id;

        internal TDefinition Definition => Instance.Definition;

        public void InitializeFromDefinition(InstanceId instanceId, TDefinition entityDefinition)
        {
            instance = new EntityInstance<TDefinition>();
            instance.Initialize(instanceId, entityDefinition);
        }

        public void InitializeFromInstance(EntityInstance<TDefinition> instance)
        {
            this.instance = instance;
        }

        public T GetVariable<T>(Variable key)
        {
            return Instance.GetVariable<T>(key);
        }

        public bool TryGetVariable<T>(Variable key, out T value)
        {
            return Instance.TryGetVariable(key, out value);
        }

        public ModifierId AddModifier(EntityModifierEntry entry)
        {
            return Instance.AddModifier(entry);
        }

        public bool RemoveModifier(Variable key, ModifierId id)
        {
            return Instance.RemoveModifier(key, id);
        }

        public void ClearModifiers()
        {
            Instance.ClearModifiers();
        }

        public IDisposable Subscribe(Variable key, Action<VariableValue> onChange)
        {
            return Instance.Subscribe(key, onChange);
        }

        public void Unsubscribe(Variable key, Action<VariableValue> onChange)
        {
            Instance.Unsubscribe(key, onChange);
        }

        public bool AddVariable(Variable key, VariableValue initialBase)
        {
            return Instance.AddVariable(key, initialBase);
        }

        public bool RemoveVariable(Variable key)
        {
            return Instance.RemoveVariable(key);
        }

        public IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler)
        {
            return Instance.SubscribeToVariableStructuralChanges(handler);
        }
    }
}

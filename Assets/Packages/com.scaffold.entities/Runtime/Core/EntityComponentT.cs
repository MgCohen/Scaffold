using System;
using UnityEngine;

namespace Scaffold.Entities
{
    public class EntityComponent<TDefinition> : EntityComponent, IInstance<TDefinition> where TDefinition : IEntityDefinition
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

        public T GetValue<T>(Variable key)
        {
            return Instance.GetValue<T>(key);
        }

        public bool TryGetValue<T>(Variable key, out T value)
        {
            return Instance.TryGetValue(key, out value);
        }

        public TVar GetVariable<TVar>(Variable key) where TVar : VariableValue
        {
            return Instance.GetVariable<TVar>(key);
        }

        public bool TryGetVariable<TVar>(Variable key, out TVar value) where TVar : VariableValue
        {
            return Instance.TryGetVariable(key, out value);
        }

        public void AddModifier(EntityModifierEntry entry)
        {
            Instance.AddModifier(entry);
        }

        public bool RemoveModifier(EntityModifierEntry entry)
        {
            return Instance.RemoveModifier(entry);
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

        public IDisposable SubscribeToVariableAdded(Action<Variable, VariableValue> onAdded)
        {
            return Instance.SubscribeToVariableAdded(onAdded);
        }

        public IDisposable SubscribeToVariableRemoved(Action<Variable> onRemoved)
        {
            return Instance.SubscribeToVariableRemoved(onRemoved);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            instance?.EditorApplyVariableAuthoringOnBagsFromValidation();

            if (!Application.isPlaying)
            {
                return;
            }

            instance?.NotifyAllEffectiveValues();
        }
#endif
    }
}

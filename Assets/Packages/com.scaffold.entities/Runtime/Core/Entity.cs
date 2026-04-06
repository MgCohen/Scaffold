using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// MonoBehaviour host for <see cref="EntityInstanceState"/>; use with <see cref="EntityBehaviorRunner{TData,TInput}"/> and gameplay code.
    /// </summary>
    public class Entity : MonoBehaviour
    {
        public InstanceId Id => instanceState.Id;

        public EntityDefinition Definition => instanceState.Definition;

        public EntityInstanceState State => instanceState;

        public IReadOnlyList<EntityModifierEntry> Modifiers => instanceState.Modifiers;

        [SerializeField]
        private EntityInstanceState instanceState = new EntityInstanceState();

        private void Awake()
        {
            instanceState.EnsureDefinitionLookup();
        }

        public void InitializeFromDefinition(EntityDefinition entityDefinition)
        {
            instanceState = EntityInstanceFactory.CreateState(entityDefinition);
        }

        public void SetState(EntityInstanceState state)
        {
            instanceState = state ?? new EntityInstanceState();
            instanceState.EnsureDefinitionLookup();
        }

        public bool TryGetAttribute(AttributeSO attribute, out Attribute value)
        {
            return instanceState.TryGetAttribute(attribute, out value);
        }

        public bool TryGetAttribute(Attribute template, out Attribute value)
        {
            return instanceState.TryGetAttribute(template, out value);
        }

        public bool TryGetAttribute(string match, out Attribute value)
        {
            return instanceState.TryGetAttribute(match, out value);
        }

        public void AddModifier(EntityModifierEntry entry)
        {
            instanceState.AddModifier(entry);
        }

        public void ClearModifiers()
        {
            instanceState.ClearModifiers();
        }

        public bool RemoveModifierAt(int index)
        {
            return instanceState.RemoveModifierAt(index);
        }
    }
}

using System;
using UnityEngine;

namespace Scaffold.Entities
{
    public class EntityComponent<TDefinition> : EntityComponent, IEntity<TDefinition> where TDefinition : EntityDefinition
    {
        public EntityInstance<TDefinition> Instance => instance;
        [SerializeField] private EntityInstance<TDefinition> instance;

        public InstanceId Id => Instance.Id;

        public TDefinition Definition => Instance.Definition;

        public void InitializeFromDefinition(InstanceId instanceId, TDefinition entityDefinition)
        {
            instance = new EntityInstance<TDefinition>();
            instance.Initialize(instanceId, entityDefinition);
        }

        public T GetValue<T>(Attribute attribute)
        {
            return Instance.GetValue<T>(attribute);
        }

        public TAttr GetAttribute<TAttr>(Attribute attribute) where TAttr : AttributeValue
        {
            return Instance.GetAttribute<TAttr>(attribute);
        }

        public bool TryGetAttribute<TAttr>(Attribute attribute, out TAttr value) where TAttr : AttributeValue
        {
            return Instance.TryGetAttribute(attribute, out value);
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

        public IDisposable Subscribe(Attribute attribute, Action<AttributeValue> onChange)
        {
            return Instance.Subscribe(attribute, onChange);
        }

        public IDisposable Subscribe<T>(Attribute attribute, Action<T> onChange)
        {
            return Instance.Subscribe(attribute, onChange);
        }

        public IDisposable SubscribeToAttribute<TAttr>(Attribute attribute, Action<TAttr> onChange) where TAttr : AttributeValue
        {
            return Instance.SubscribeToAttribute(attribute, onChange);
        }

        public void Unsubscribe(Attribute attribute, Action<AttributeValue> onChange)
        {
            Instance.Unsubscribe(attribute, onChange);
        }

        public bool AddRuntimeAttribute(Attribute key, AttributeValue initialBase)
        {
            return Instance.AddRuntimeAttribute(key, initialBase);
        }

        public bool RemoveRuntimeAttribute(Attribute key)
        {
            return Instance.RemoveRuntimeAttribute(key);
        }

        public IDisposable SubscribeToAttributeAdded(Action<Attribute, AttributeValue> onAdded)
        {
            return Instance.SubscribeToAttributeAdded(onAdded);
        }

        public IDisposable SubscribeToAttributeRemoved(Action<Attribute> onRemoved)
        {
            return Instance.SubscribeToAttributeRemoved(onRemoved);
        }
    }
}

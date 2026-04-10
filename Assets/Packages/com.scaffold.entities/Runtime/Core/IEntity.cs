using System;

namespace Scaffold.Entities
{
    public interface IEntity<out TDefinition> where TDefinition : EntityDefinition
    {
        InstanceId Id { get; }

        TDefinition Definition { get; }

        T GetValue<T>(Attribute attribute);

        TAttr GetAttribute<TAttr>(Attribute attribute) where TAttr : AttributeValue;

        bool TryGetAttribute<TAttr>(Attribute attribute, out TAttr value) where TAttr : AttributeValue;

        void AddModifier(EntityModifierEntry entry);

        bool RemoveModifier(EntityModifierEntry entry);

        void ClearModifiers();

        IDisposable Subscribe(Attribute attribute, Action<AttributeValue> onChange);

        IDisposable Subscribe<T>(Attribute attribute, Action<T> onChange);

        IDisposable SubscribeToAttribute<TAttr>(Attribute attribute, Action<TAttr> onChange) where TAttr : AttributeValue;

        void Unsubscribe(Attribute attribute, Action<AttributeValue> onChange);
    }
}

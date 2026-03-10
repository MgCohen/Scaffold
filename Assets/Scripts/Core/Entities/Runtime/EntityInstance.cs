using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class EntityInstance<TDefinition> : IEntityInstance where TDefinition : EntityDefinition
    {
        public string Id;
        public TDefinition DefinitionRef;
        public List<EntityModifier> ModifiersRef = new List<EntityModifier>();

        string IEntityInstance.Id { get { return Id; } }
        EntityDefinition IEntityInstance.Definition { get { return DefinitionRef; } }
        IReadOnlyList<EntityModifier> IEntityInstance.Modifiers { get { return ModifiersRef; } }

        public bool TryGetAttributeValue(string key, out double value)
        {
            value = default;
            bool hasBase = TryGetBaseValue(key, out double baseValue);
            if (!hasBase) { return false; }
            bool hasModifier = HasModifierForKey(key);
            if (!hasModifier) { value = baseValue; return true; }
            value = ApplyModifiers(key, baseValue);
            return true;
        }

        private bool TryGetBaseValue(string key, out double baseValue)
        {
            baseValue = default;
            if (DefinitionRef == null) { return false; }
            return DefinitionRef.TryGetBaseAttributeValue(key, out baseValue);
        }

        private bool HasModifierForKey(string key)
        {
            List<EntityModifier> modifiers = GetModifiers();
            for (int index = 0; index < modifiers.Count; index++) { EntityModifier modifier = modifiers[index]; if (modifier != null && modifier.TargetsAttribute(key)) { return true; } }
            return false;
        }

        private double ApplyModifiers(string key, double baseValue)
        {
            double result = baseValue;
            List<EntityModifier> modifiers = GetModifiers();
            for (int index = 0; index < modifiers.Count; index++) { EntityModifier modifier = modifiers[index]; if (modifier != null && modifier.TargetsAttribute(key)) { result = modifier.Apply(result); } }
            return result;
        }

        private List<EntityModifier> GetModifiers()
        {
            if (ModifiersRef == null) { ModifiersRef = new List<EntityModifier>(); }
            return ModifiersRef;
        }
    }
}

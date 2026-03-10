using System.Collections.Generic;

namespace Scaffold.Entities.Samples
{
    public static class EntitiesUseCases
    {
        public static bool UseCase()
        {
            EntityDefinition definition = BuildDefinition();
            EntityInstance<EntityDefinition> instance = BuildInstance(definition);
            IEntityRegistry registry = new EntityRegistry();
            bool didRegisterDefinition = registry.RegisterDefinition(definition);
            bool didRegisterInstance = registry.RegisterInstance(instance);
            bool didResolve = instance.TryGetAttributeValue("Strength", out double value);
            return didRegisterDefinition && didRegisterInstance && didResolve && value == 6d;
        }

        private static EntityDefinition BuildDefinition()
        {
            EntityDefinition definition = new EntityDefinition();
            definition.Id = "orc_definition";
            definition.DisplayName = "Orc";
            definition.Attributes = new List<EntityAttribute>();
            definition.Attributes.Add(new EntityAttribute { Key = "Strength", Value = 5d });
            return definition;
        }

        private static EntityInstance<EntityDefinition> BuildInstance(EntityDefinition definition)
        {
            EntityInstance<EntityDefinition> instance = new EntityInstance<EntityDefinition>();
            instance.Id = "orc_instance";
            instance.DefinitionRef = definition;
            instance.ModifiersRef = new List<EntityModifier>();
            instance.ModifiersRef.Add(new AddAttributeModifier { Id = "buff_strength", TargetAttributeKey = "Strength", Amount = 1d, IsTemporary = true });
            return instance;
        }
    }
}

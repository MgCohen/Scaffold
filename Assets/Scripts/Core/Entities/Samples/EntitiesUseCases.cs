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
            definition.Attributes = new Dictionary<string, EntityAttribute>();
            definition.Attributes["Strength"] = new EntityAttribute { Key = "Strength", Value = 5d };
            return definition;
        }

        private static EntityInstance<EntityDefinition> BuildInstance(EntityDefinition definition)
        {
            EntityInstance<EntityDefinition> instance = new EntityInstance<EntityDefinition>();
            instance.Id = "orc_instance";
            instance.Definition = definition;
            AddAttributeModifier modifier = new AddAttributeModifier { Amount = 1d };
            instance.AddModifier("Strength", modifier);
            return instance;
        }
    }
}

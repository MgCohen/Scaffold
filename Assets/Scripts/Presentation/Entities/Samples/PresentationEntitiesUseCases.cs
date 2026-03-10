using System.Collections.Generic;
using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Presentation.Entities.Samples
{
    public static class PresentationEntitiesUseCases
    {
        public static bool UseCase()
        {
            EntityDefinition definition = BuildDefinition();
            EntityInstance<EntityDefinition> instance = BuildInstance(definition);
            EntityDefinitionAsset definitionAsset = definition;
            EntityInstanceAsset instanceAsset = instance;
            bool hasDefinition = definitionAsset != null;
            bool hasInstance = instanceAsset != null;
            return hasDefinition && hasInstance;
        }

        private static EntityDefinition BuildDefinition()
        {
            EntityDefinition definition = new EntityDefinition();
            definition.Id = "sample_definition";
            definition.Attributes = new List<EntityAttribute>();
            definition.Attributes.Add(new EntityAttribute { Key = "Strength", Value = 5d });
            return definition;
        }

        private static EntityInstance<EntityDefinition> BuildInstance(EntityDefinition definition)
        {
            EntityInstance<EntityDefinition> instance = new EntityInstance<EntityDefinition>();
            instance.Id = "sample_instance";
            instance.DefinitionRef = definition;
            instance.ModifiersRef = new List<EntityModifier>();
            return instance;
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Presentation.Entities.Tests
{
    public class PresentationEntitiesTests
    {
        [Test]
        public void DefinitionAsset_ImplicitConversion_RoundTripsDefinition()
        {
            EntityDefinition definition = BuildDefinition();
            EntityDefinitionAsset asset = definition;
            EntityDefinition loaded = asset;
            Assert.IsNotNull(asset);
            Assert.AreEqual("definition_asset", loaded.Id);
        }

        [Test]
        public void InstanceAsset_ImplicitConversion_RoundTripsInstance()
        {
            EntityDefinition definition = BuildDefinition();
            EntityInstance<EntityDefinition> instance = BuildInstance(definition);
            EntityInstanceAsset asset = instance;
            EntityInstance<EntityDefinition> loaded = asset;
            Assert.IsNotNull(asset);
            Assert.AreEqual("instance_asset", loaded.Id);
        }

        [Test]
        public void AttributeAsset_ImplicitConversion_RoundTripsAttribute()
        {
            EntityAttribute attribute = new EntityAttribute();
            attribute.Key = "Strength";
            attribute.Value = 5d;
            EntityAttributeAsset asset = attribute;
            EntityAttribute loaded = asset;
            Assert.IsNotNull(asset);
            Assert.AreEqual("Strength", loaded.Key);
        }

        [Test]
        public void ModifierAsset_ImplicitConversion_RoundTripsModifier()
        {
            AddAttributeModifier modifier = new AddAttributeModifier();
            modifier.Amount = 2d;
            EntityModifierAsset asset = modifier;
            EntityModifier loaded = asset;
            Assert.IsNotNull(asset);
            Assert.IsNotNull(loaded);
        }

        [TearDown]
        public void TearDown()
        {
            Resources.UnloadUnusedAssets();
        }

        private EntityDefinition BuildDefinition()
        {
            EntityDefinition definition = new EntityDefinition();
            definition.Id = "definition_asset";
            definition.Attributes = new Dictionary<string, EntityAttribute>();
            definition.Attributes["Strength"] = new EntityAttribute { Key = "Strength", Value = 5d };
            return definition;
        }

        private EntityInstance<EntityDefinition> BuildInstance(EntityDefinition definition)
        {
            EntityInstance<EntityDefinition> instance = new EntityInstance<EntityDefinition>();
            instance.Id = "instance_asset";
            instance.Definition = definition;
            return instance;
        }
    }
}

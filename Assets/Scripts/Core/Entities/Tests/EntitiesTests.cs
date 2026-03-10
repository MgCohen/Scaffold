using System.Collections.Generic;
using NUnit.Framework;

namespace Scaffold.Entities.Tests
{
    public class EntitiesTests
    {
        [Test]
        public void Definition_TryGetBaseAttributeValue_ReturnsExpectedValue()
        {
            EntityDefinition definition = BuildDefinition("orc_definition", 5d, 3d);
            bool found = definition.TryGetBaseAttributeValue("Strength", out double value);
            Assert.IsTrue(found);
            Assert.AreEqual(5d, value);
        }

        [Test]
        public void Instance_WithoutModifiers_ReturnsBaseValue()
        {
            EntityDefinition definition = BuildDefinition("orc_definition", 5d, 3d);
            EntityInstance<EntityDefinition> instance = BuildInstance("orc_instance", definition);
            bool found = instance.TryGetAttributeValue("Strength", out double value);
            Assert.IsTrue(found);
            Assert.AreEqual(5d, value);
        }

        [Test]
        public void Instance_WithAddModifier_ReturnsModifiedValue()
        {
            EntityDefinition definition = BuildDefinition("orc_definition", 5d, 3d);
            EntityInstance<EntityDefinition> instance = BuildInstance("orc_instance", definition);
            instance.ModifiersRef.Add(new AddAttributeModifier { Id = "add_strength", TargetAttributeKey = "Strength", Amount = 1d, IsTemporary = true });
            bool found = instance.TryGetAttributeValue("Strength", out double value);
            Assert.IsTrue(found);
            Assert.AreEqual(6d, value);
        }

        [Test]
        public void Instance_WithMultiplyModifier_ReturnsModifiedValue()
        {
            EntityDefinition definition = BuildDefinition("orc_definition", 5d, 3d);
            EntityInstance<EntityDefinition> instance = BuildInstance("orc_instance", definition);
            instance.ModifiersRef.Add(new MultiplyAttributeModifier { Id = "mult_speed", TargetAttributeKey = "Speed", Factor = 2d, IsTemporary = true });
            bool found = instance.TryGetAttributeValue("Speed", out double value);
            Assert.IsTrue(found);
            Assert.AreEqual(6d, value);
        }

        [Test]
        public void Instance_WithAddAndRemoveModifiers_AppliesInOrder()
        {
            EntityDefinition definition = BuildDefinition("orc_definition", 5d, 3d);
            EntityInstance<EntityDefinition> instance = BuildInstance("orc_instance", definition);
            instance.ModifiersRef.Add(new AddAttributeModifier { Id = "add_strength", TargetAttributeKey = "Strength", Amount = 3d, IsTemporary = true });
            instance.ModifiersRef.Add(new RemoveAttributeModifier { Id = "remove_strength", TargetAttributeKey = "Strength", Amount = 1d, IsTemporary = true });
            bool found = instance.TryGetAttributeValue("Strength", out double value);
            Assert.IsTrue(found);
            Assert.AreEqual(7d, value);
        }

        [Test]
        public void Registry_RejectsDuplicateIdsAcrossDefinitionAndInstance()
        {
            EntityRegistry registry = new EntityRegistry();
            EntityDefinition definition = BuildDefinition("shared_id", 5d, 3d);
            EntityInstance<EntityDefinition> instance = BuildInstance("shared_id", definition);
            bool definitionRegistered = registry.RegisterDefinition(definition);
            bool instanceRegistered = registry.RegisterInstance(instance);
            Assert.IsTrue(definitionRegistered);
            Assert.IsFalse(instanceRegistered);
        }

        [Test]
        public void Registry_RejectsInstanceWithUnknownDefinition()
        {
            EntityRegistry registry = new EntityRegistry();
            EntityDefinition definition = BuildDefinition("orc_definition", 5d, 3d);
            EntityInstance<EntityDefinition> instance = BuildInstance("orc_instance", definition);
            bool instanceRegistered = registry.RegisterInstance(instance);
            Assert.IsFalse(instanceRegistered);
        }

        [Test]
        public void Registry_FindsDefinitionAndInstanceById()
        {
            EntityRegistry registry = new EntityRegistry();
            EntityDefinition definition = BuildDefinition("orc_definition", 5d, 3d);
            EntityInstance<EntityDefinition> instance = BuildInstance("orc_instance", definition);
            bool definitionRegistered = registry.RegisterDefinition(definition);
            bool instanceRegistered = registry.RegisterInstance(instance);
            AssertLookupState(registry, definitionRegistered, instanceRegistered);
        }

        private EntityDefinition BuildDefinition(string id, double strength, double speed)
        {
            EntityDefinition definition = new EntityDefinition();
            definition.Id = id;
            definition.DisplayName = "Unit";
            definition.Attributes = new List<EntityAttribute>();
            definition.Attributes.Add(new EntityAttribute { Key = "Strength", Value = strength });
            definition.Attributes.Add(new EntityAttribute { Key = "Speed", Value = speed });
            return definition;
        }

        private EntityInstance<EntityDefinition> BuildInstance(string id, EntityDefinition definition)
        {
            EntityInstance<EntityDefinition> instance = new EntityInstance<EntityDefinition>();
            instance.Id = id;
            instance.DefinitionRef = definition;
            instance.ModifiersRef = new List<EntityModifier>();
            return instance;
        }

        private void AssertLookupState(EntityRegistry registry, bool definitionRegistered, bool instanceRegistered)
        {
            bool foundDefinition = registry.TryGetDefinition("orc_definition", out EntityDefinition loadedDefinition);
            bool foundInstance = registry.TryGetInstance("orc_instance", out IEntityInstance loadedInstance);
            Assert.IsTrue(definitionRegistered);
            Assert.IsTrue(instanceRegistered);
            Assert.IsTrue(foundDefinition);
            Assert.IsTrue(foundInstance);
            Assert.AreEqual("orc_definition", loadedDefinition.Id);
            Assert.AreEqual("orc_instance", loadedInstance.Id);
        }
    }
}

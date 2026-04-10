using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Entities.Tests
{
    public sealed class AttributeValueKindRegistryTests
    {
        [TearDown]
        public void TearDown()
        {
            AttributeValueKinds.SetGlobalRegistry(null);
        }

        [Test]
        public void EnsureValueMatchesType_UsesRegistryByStableId_NoReflection()
        {
            var def = new FloatScalarAttributeDefinition();
            def.SetStableTypeId("MyFloat");

            var registry = ScriptableObject.CreateInstance<AttributeValueKindRegistrySO>();
            registry.SetKindsForTests(new List<AttributeDefinitionBase> { def });

            AttributeSO so = CreateAttributeSo("HP", AttributeValueType.Float);
            so.SetValueKindId("MyFloat");
            so.SetKindRegistryOverride(registry);

            AttributeEntry entry = AttributeEntry.Create(so, null);
            entry.EnsureValueMatchesType();

            Assert.That(entry.BaseValue, Is.TypeOf<FloatAttributeValue>());
        }

        [Test]
        public void EnsureValueMatchesType_RegistryFallsBackToLegacyEnumMapping()
        {
            var registry = ScriptableObject.CreateInstance<AttributeValueKindRegistrySO>();
            registry.SetKindsForTests(new List<AttributeDefinitionBase> { new FloatScalarAttributeDefinition() });

            AttributeSO so = CreateAttributeSo("HP", AttributeValueType.Float);
            so.SetKindRegistryOverride(registry);

            AttributeEntry entry = AttributeEntry.Create(so, null);
            entry.EnsureValueMatchesType();

            Assert.That(entry.BaseValue, Is.TypeOf<FloatAttributeValue>());
        }

        private static AttributeSO CreateAttributeSo(string assetName, AttributeValueType valueType)
        {
            var so = ScriptableObject.CreateInstance<AttributeSO>();
            so.name = assetName;
            so.SetValueType(valueType);
            return so;
        }
    }
}

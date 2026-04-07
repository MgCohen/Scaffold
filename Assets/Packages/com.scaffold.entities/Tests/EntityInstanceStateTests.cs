using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Entities.Tests
{
    public sealed class EntityInstanceStateTests
    {
        private static AttributeSO CreateAttributeSo(string assetName, string defaultPayload)
        {
            var so = ScriptableObject.CreateInstance<AttributeSO>();
            so.name = assetName;
            FieldInfo field = typeof(AttributeSO).GetField(
                "defaultPayload",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(so, defaultPayload);
            return so;
        }

        private static EntityDefinition CreateDefinition(params (AttributeSO so, string overridePayload)[] entries)
        {
            var def = ScriptableObject.CreateInstance<EntityDefinition>();
            def.name = "TestDefinition";
            FieldInfo listField = typeof(EntityDefinition).GetField(
                "defaultAttributes",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(listField);
            var list = (List<EntityDefinitionDefaultEntry>)listField.GetValue(def);
            foreach ((AttributeSO so, string o) in entries)
            {
                var entry = new EntityDefinitionDefaultEntry();
                FieldInfo attrField = typeof(EntityDefinitionDefaultEntry).GetField(
                    "attribute",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo payField = typeof(EntityDefinitionDefaultEntry).GetField(
                    "payloadOverride",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                attrField.SetValue(entry, so);
                payField.SetValue(entry, o);
                list.Add(entry);
            }

            def.RebuildLookup();
            return def;
        }

        [Test]
        public void TryGetAttribute_ByAttributeSO_ReturnsDefinitionDefault()
        {
            AttributeSO hp = CreateAttributeSo("HP", "100");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);

            Assert.That(state.TryGetAttribute(hp, out Attribute a), Is.True);
            Assert.That(a.Payload, Is.EqualTo("100"));
            Assert.That(a.MatchKey, Is.EqualTo("HP"));
        }

        [Test]
        public void TryGetAttribute_ByAttributeTemplate_UsesMatchKey()
        {
            AttributeSO hp = CreateAttributeSo("HP", "100");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);
            Attribute template = (Attribute)hp;

            Assert.That(state.TryGetAttribute(template, out Attribute a), Is.True);
            Assert.That(a.Payload, Is.EqualTo("100"));
        }

        [Test]
        public void TryGetAttribute_ByString_FindsByAssetName()
        {
            AttributeSO hp = CreateAttributeSo("HP", "100");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);

            Assert.That(state.TryGetAttribute("HP", out Attribute a), Is.True);
            Assert.That(a.Payload, Is.EqualTo("100"));
        }

        [Test]
        public void Modifiers_OnInstance_CombineNumericPayloads()
        {
            AttributeSO hp = CreateAttributeSo("HP", "10");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);
            state.AddModifier(new EntityModifierEntry(hp, "5"));

            Assert.That(state.TryGetAttribute(hp, out Attribute a), Is.True);
            Assert.That(a.Payload, Is.EqualTo("15"));
        }

        [Test]
        public void Modifiers_DoNotMutateDefinition()
        {
            AttributeSO hp = CreateAttributeSo("HP", "10");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);
            state.AddModifier(new EntityModifierEntry(hp, "5"));

            Assert.That(def.GetBaseAttribute(hp).Payload, Is.EqualTo("10"));
        }

        [Test]
        public void Factory_AssignsNonEmptyInstanceId()
        {
            AttributeSO hp = CreateAttributeSo("HP", "1");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);

            Assert.That(state.Id.IsEmpty, Is.False);
        }

        [Test]
        public void RemoveModifierAt_AfterNumericCombine_RestoresDefinitionBase()
        {
            AttributeSO hp = CreateAttributeSo("HP", "10");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);
            state.AddModifier(new EntityModifierEntry(hp, "5"));

            Assert.That(state.TryGetAttribute(hp, out Attribute combined), Is.True);
            Assert.That(combined.Payload, Is.EqualTo("15"));

            Assert.That(state.RemoveModifierAt(0), Is.True);
            Assert.That(state.TryGetAttribute(hp, out Attribute restored), Is.True);
            Assert.That(restored.Payload, Is.EqualTo("10"));
        }

        [Test]
        public void ClearModifiers_RemovesAllContributionsRestoresDefinitionBase()
        {
            AttributeSO hp = CreateAttributeSo("HP", "10");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);
            state.AddModifier(new EntityModifierEntry(hp, "4"));
            state.AddModifier(new EntityModifierEntry(hp, "1"));

            Assert.That(state.TryGetAttribute(hp, out Attribute stacked), Is.True);
            Assert.That(stacked.Payload, Is.EqualTo("15"));

            state.ClearModifiers();
            Assert.That(state.TryGetAttribute(hp, out Attribute cleared), Is.True);
            Assert.That(cleared.Payload, Is.EqualTo("10"));
        }

        [Test]
        public void RemoveModifierAt_InvalidIndex_ReturnsFalse()
        {
            AttributeSO hp = CreateAttributeSo("HP", "10");
            EntityDefinition def = CreateDefinition((hp, null));
            EntityInstanceState state = EntityInstanceFactory.CreateState(def);

            Assert.That(state.RemoveModifierAt(0), Is.False);
            Assert.That(state.RemoveModifierAt(-1), Is.False);
        }

        [Test]
        public void CreateOnGameObject_Entity_TryGetAttribute_UsesDefinitionBase()
        {
            AttributeSO hp = CreateAttributeSo("HP", "42");
            EntityDefinition def = CreateDefinition((hp, null));
            GameObject go = new GameObject("EntityFactoryTest");
            try
            {
                Entity entity = EntityInstanceFactory.CreateOnGameObject<Entity>(go, def);

                Assert.That(entity.State.Id.IsEmpty, Is.False);
                Assert.That(entity.TryGetAttribute(hp, out Attribute a), Is.True);
                Assert.That(a.Payload, Is.EqualTo("42"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Entities.Tests
{
    public sealed class EntityInstanceTests
    {
        private EntityInstanceCreator<EntityDefinition> creator = default!;

        [SetUp]
        public void SetUp()
        {
            creator = new EntityInstanceCreator<EntityDefinition>(new IncrementingInstanceIdGenerator());
        }

        [Test]
        public void TryGetAttribute_ByAttributeSO_ReturnsDefinitionDefault()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 100f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.TryGetAttribute(hp, out AttributeValue v), Is.True);
            Assert.That(v, Is.TypeOf<FloatAttributeValue>());
            Assert.That(((FloatAttributeValue)v).Value, Is.EqualTo(100f));
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(100f));
        }

        [Test]
        public void Modifiers_OnInstance_SumFloatValues()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var delta = new FloatAttributeValue { Value = 5f };
            var mod = new EntityModifierEntry(hp, delta);
            state.AddModifier(mod);

            Assert.That(state.GetValue<float>(hp), Is.EqualTo(15f));
        }

        [Test]
        public void Modifiers_DoNotMutateDefinition()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var delta = new FloatAttributeValue { Value = 5f };
            state.AddModifier(new EntityModifierEntry(hp, delta));

            Assert.That(def.TryGetBaseValue((Attribute)hp, out AttributeValue baseV), Is.True);
            Assert.That(((FloatAttributeValue)baseV).Value, Is.EqualTo(10f));
        }

        [Test]
        public void Creator_AssignsNonEmptyInstanceId()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 1f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.Id.Id, Is.GreaterThan(0));
        }

        [Test]
        public void RemoveModifier_AfterNumericCombine_RestoresDefinitionBase()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var delta = new FloatAttributeValue { Value = 5f };
            var mod = new EntityModifierEntry(hp, delta);
            state.AddModifier(mod);

            Assert.That(state.GetValue<float>(hp), Is.EqualTo(15f));

            Assert.That(state.RemoveModifier(mod), Is.True);
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void ClearModifiers_RemovesAllContributionsRestoresDefinitionBase()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var d1 = new FloatAttributeValue { Value = 4f };
            var d2 = new FloatAttributeValue { Value = 1f };
            state.AddModifier(new EntityModifierEntry(hp, d1));
            state.AddModifier(new EntityModifierEntry(hp, d2));

            Assert.That(state.GetValue<float>(hp), Is.EqualTo(15f));

            state.ClearModifiers();
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void RemoveModifier_UnknownEntry_ReturnsFalse()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var neverAdded = new EntityModifierEntry(hp, new FloatAttributeValue { Value = 5f });

            Assert.That(state.RemoveModifier(neverAdded), Is.False);
            Assert.That(state.RemoveModifier(null), Is.False);
        }

        [Test]
        public void Subscribe_FiresImmediatelyWithCurrentValue()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 50f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            AttributeValue received = null;
            state.Subscribe(hp, v => received = v);

            Assert.That(received, Is.Not.Null);
            Assert.That(((FloatAttributeValue)received).Value, Is.EqualTo(50f));
        }

        [Test]
        public void Subscribe_FiresOnModifierAdd()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            AttributeValue received = null;
            state.Subscribe(hp, v => received = v);

            state.AddModifier(new EntityModifierEntry(hp, new FloatAttributeValue { Value = 5f }));

            Assert.That(received, Is.Not.Null);
            Assert.That(((FloatAttributeValue)received).Value, Is.EqualTo(15f));
        }

        [Test]
        public void Subscribe_FiresOnModifierRemove()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var mod = new EntityModifierEntry(hp, new FloatAttributeValue { Value = 5f });
            state.AddModifier(mod);

            AttributeValue received = null;
            state.Subscribe(hp, v => received = v);

            state.RemoveModifier(mod);

            Assert.That(received, Is.Not.Null);
            Assert.That(((FloatAttributeValue)received).Value, Is.EqualTo(10f));
        }

        [Test]
        public void Subscribe_DoesNotFireAfterUnsubscribe()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            void OnChange(AttributeValue v) => callCount++;

            state.Subscribe(hp, OnChange);
            Assert.That(callCount, Is.EqualTo(1)); // immediate fire

            state.Unsubscribe(hp, OnChange);
            state.AddModifier(new EntityModifierEntry(hp, new FloatAttributeValue { Value = 5f }));

            Assert.That(callCount, Is.EqualTo(1)); // no additional fires
        }

        [Test]
        public void Subscribe_Float_FiresImmediatelyWithTypedValue()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 42f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            float received = 0f;
            using (state.Subscribe<float>(hp, v => received = v))
            {
                Assert.That(received, Is.EqualTo(42f));
            }
        }

        [Test]
        public void Subscribe_Float_FiresOnModifierAdd()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            float received = 0f;
            using (state.Subscribe<float>(hp, v => received = v))
            {
                state.AddModifier(new EntityModifierEntry(hp, new FloatAttributeValue { Value = 5f }));
                Assert.That(received, Is.EqualTo(15f));
            }
        }

        [Test]
        public void SubscribeToAttribute_FloatAttributeValue_FiresImmediately()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 33f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            FloatAttributeValue received = null!;
            using (state.SubscribeToAttribute<FloatAttributeValue>(hp, v => received = v))
            {
                Assert.That(received, Is.Not.Null);
                Assert.That(received.Value, Is.EqualTo(33f));
            }
        }

        [Test]
        public void Subscribe_Typed_DisposeStopsFurtherNotifications()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            IDisposable sub = state.Subscribe<float>(hp, _ => callCount++);
            Assert.That(callCount, Is.EqualTo(1));

            sub.Dispose();
            state.AddModifier(new EntityModifierEntry(hp, new FloatAttributeValue { Value = 5f }));

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_Untyped_ReturnsTokenThatDisposes()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            IDisposable sub = state.Subscribe(hp, _ => callCount++);
            Assert.That(callCount, Is.EqualTo(1));

            sub.Dispose();
            state.AddModifier(new EntityModifierEntry(hp, new FloatAttributeValue { Value = 5f }));

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void EffectiveBag_NoModifierLayer_ForDefinitionAttributeWithoutModifiers()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.ContainsModifiedValueCache(hp), Is.False);
            Assert.That(state.InstanceBagHasLocalKey(hp), Is.False);
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void AddRuntimeAttribute_ThenRead_WorksAndCachesWhenModifiersApplied()
        {
            AttributeSO poison = CreateAttributeSo("Poison", AttributeValueType.Float);
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.AddRuntimeAttribute(poison, new FloatAttributeValue { Value = 5f }), Is.True);
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(5f));
            Assert.That(state.InstanceBagHasLocalKey(poison), Is.True);

            state.AddModifier(new EntityModifierEntry(poison, new FloatAttributeValue { Value = 2f }));
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(7f));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.True);
        }

        [Test]
        public void RuntimeAttribute_AfterModifierRemoved_ReadsOriginalBase()
        {
            AttributeSO poison = CreateAttributeSo("Poison", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddRuntimeAttribute(poison, new FloatAttributeValue { Value = 5f });
            var mod = new EntityModifierEntry(poison, new FloatAttributeValue { Value = 2f });
            state.AddModifier(mod);
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(7f));

            Assert.That(state.RemoveModifier(mod), Is.True);
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(5f));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.False);
            Assert.That(state.InstanceBagHasLocalKey(poison), Is.True);
        }

        [Test]
        public void RemoveRuntimeAttribute_ClearsModifiersAndStructuralSubscriptionFires()
        {
            AttributeSO poison = CreateAttributeSo("Poison", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddRuntimeAttribute(poison, new FloatAttributeValue { Value = 5f });
            state.AddModifier(new EntityModifierEntry(poison, new FloatAttributeValue { Value = 1f }));

            var removed = new List<Scaffold.Entities.Attribute>();
            using (state.SubscribeToAttributeRemoved(removed.Add))
            {
                Assert.That(state.RemoveRuntimeAttribute(poison), Is.True);
            }

            Assert.That(removed.Count, Is.EqualTo(1));
            Assert.That(removed[0], Is.EqualTo((Scaffold.Entities.Attribute)poison));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.False);
        }

        [Test]
        public void SubscribeToAttributeAdded_FiresWhenRuntimeSlotAdded()
        {
            AttributeSO poison = CreateAttributeSo("Poison", AttributeValueType.Float);
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Scaffold.Entities.Attribute seen = null!;
            using (state.SubscribeToAttributeAdded((k, _) => seen = k))
            {
                state.AddRuntimeAttribute(poison, new FloatAttributeValue { Value = 1f });
            }

            Assert.That(seen, Is.EqualTo((Scaffold.Entities.Attribute)poison));
        }

        private AttributeSO CreateAttributeSo(string assetName, AttributeValueType valueType)
        {
            var so = ScriptableObject.CreateInstance<AttributeSO>();
            so.name = assetName;
            so.SetValueType(valueType);
            return so;
        }

        private EntityDefinition CreateDefinition(params (AttributeSO so, AttributeValue baseValue)[] rows)
        {
            var def = ScriptableObject.CreateInstance<EntityDefinition>();
            def.name = "TestDefinition";
            foreach ((AttributeSO so, AttributeValue bv) in rows)
            {
                def.AddEntry(AttributeEntry.Create(so, bv));
            }

            def.RebuildLookup();
            return def;
        }
    }
}

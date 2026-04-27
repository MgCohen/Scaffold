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
        public void TryGetVariable_ByVariableSO_ReturnsDefinitionDefault()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 100f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.TryGetVariable(hp, out VariableValue v), Is.True);
            Assert.That(v, Is.TypeOf<FloatVariableValue>());
            Assert.That(((FloatVariableValue)v).Value, Is.EqualTo(100f));
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(100f));
        }

        [Test]
        public void TryGetValue_WhenPresent_ReturnsTypedValue()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 42f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.TryGetValue(hp, out float value), Is.True);
            Assert.That(value, Is.EqualTo(42f));
        }

        [Test]
        public void TryGetValue_WhenMissing_ReturnsFalse()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.TryGetValue(hp, out float _), Is.False);
        }

        [Test]
        public void TryGetValue_WhenTypeMismatch_ReturnsFalse()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.TryGetValue(hp, out int _), Is.False);
        }

        [Test]
        public void Modifiers_OnInstance_SumFloatValues()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var delta = new FloatVariableValue { Value = 5f };
            var mod = new EntityModifierEntry(hp, delta);
            state.AddModifier(mod);

            Assert.That(state.GetValue<float>(hp), Is.EqualTo(15f));
        }

        [Test]
        public void Modifiers_DoNotMutateDefinition()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var delta = new FloatVariableValue { Value = 5f };
            state.AddModifier(new EntityModifierEntry(hp, delta));

            Assert.That(def.TryGetDefaultValue((Variable)hp, out VariableValue baseV), Is.True);
            Assert.That(((FloatVariableValue)baseV).Value, Is.EqualTo(10f));
        }

        [Test]
        public void Creator_AssignsNonEmptyInstanceId()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 1f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.Id.Id, Is.GreaterThan(0));
        }

        [Test]
        public void RemoveModifier_AfterNumericCombine_RestoresDefinitionBase()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var delta = new FloatVariableValue { Value = 5f };
            var mod = new EntityModifierEntry(hp, delta);
            state.AddModifier(mod);

            Assert.That(state.GetValue<float>(hp), Is.EqualTo(15f));

            Assert.That(state.RemoveModifier(mod), Is.True);
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void ClearModifiers_RemovesAllContributionsRestoresDefinitionBase()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var d1 = new FloatVariableValue { Value = 4f };
            var d2 = new FloatVariableValue { Value = 1f };
            state.AddModifier(new EntityModifierEntry(hp, d1));
            state.AddModifier(new EntityModifierEntry(hp, d2));

            Assert.That(state.GetValue<float>(hp), Is.EqualTo(15f));

            state.ClearModifiers();
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void RemoveModifier_UnknownEntry_ReturnsFalse()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var neverAdded = new EntityModifierEntry(hp, new FloatVariableValue { Value = 5f });

            Assert.That(state.RemoveModifier(neverAdded), Is.False);
            Assert.That(state.RemoveModifier(null), Is.False);
        }

        [Test]
        public void Subscribe_FiresImmediatelyWithCurrentValue()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 50f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            VariableValue received = null!;
            state.Subscribe(hp, v => received = v);

            Assert.That(received, Is.Not.Null);
            Assert.That(((FloatVariableValue)received).Value, Is.EqualTo(50f));
        }

        [Test]
        public void Subscribe_FiresOnModifierAdd()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            VariableValue received = null!;
            state.Subscribe(hp, v => received = v);

            state.AddModifier(new EntityModifierEntry(hp, new FloatVariableValue { Value = 5f }));

            Assert.That(received, Is.Not.Null);
            Assert.That(((FloatVariableValue)received).Value, Is.EqualTo(15f));
        }

        [Test]
        public void Subscribe_FiresOnModifierRemove()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var mod = new EntityModifierEntry(hp, new FloatVariableValue { Value = 5f });
            state.AddModifier(mod);

            VariableValue received = null!;
            state.Subscribe(hp, v => received = v);

            state.RemoveModifier(mod);

            Assert.That(received, Is.Not.Null);
            Assert.That(((FloatVariableValue)received).Value, Is.EqualTo(10f));
        }

        [Test]
        public void Subscribe_DoesNotFireAfterUnsubscribe()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            void OnChange(VariableValue v) => callCount++;

            state.Subscribe(hp, OnChange);
            Assert.That(callCount, Is.EqualTo(1));

            state.Unsubscribe(hp, OnChange);
            state.AddModifier(new EntityModifierEntry(hp, new FloatVariableValue { Value = 5f }));

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_Float_FiresImmediatelyWithTypedValue()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 42f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            float received = 0f;
            using (state.Subscribe(hp, (float v) => received = v))
            {
                Assert.That(received, Is.EqualTo(42f));
            }
        }

        [Test]
        public void Subscribe_Float_FiresOnModifierAdd()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            float received = 0f;
            using (state.Subscribe(hp, (float v) => received = v))
            {
                state.AddModifier(new EntityModifierEntry(hp, new FloatVariableValue { Value = 5f }));
                Assert.That(received, Is.EqualTo(15f));
            }
        }

        [Test]
        public void SubscribeToVariable_FloatVariableValue_FiresImmediately()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 33f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            FloatVariableValue received = null!;
            using (state.SubscribeToVariable(hp, (FloatVariableValue v) => received = v))
            {
                Assert.That(received, Is.Not.Null);
                Assert.That(received.Value, Is.EqualTo(33f));
            }
        }

        [Test]
        public void Subscribe_Typed_DisposeStopsFurtherNotifications()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            IDisposable sub = state.Subscribe(hp, (float _) => callCount++);
            Assert.That(callCount, Is.EqualTo(1));

            sub.Dispose();
            state.AddModifier(new EntityModifierEntry(hp, new FloatVariableValue { Value = 5f }));

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_Untyped_ReturnsTokenThatDisposes()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            IDisposable sub = state.Subscribe(hp, _ => callCount++);
            Assert.That(callCount, Is.EqualTo(1));

            sub.Dispose();
            state.AddModifier(new EntityModifierEntry(hp, new FloatVariableValue { Value = 5f }));

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void EffectiveBag_NoModifierLayer_ForDefinitionVariableWithoutModifiers()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.ContainsModifiedValueCache(hp), Is.False);
            Assert.That(state.InstanceBagHasLocalKey(hp), Is.False);
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void AddVariable_ThenRead_WorksAndCachesWhenModifiersApplied()
        {
            VariableSO poison = CreateVariableSo("Poison", VariableValueType.Float);
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.AddVariable(poison, new FloatVariableValue { Value = 5f }), Is.True);
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(5f));
            Assert.That(state.InstanceBagHasLocalKey(poison), Is.True);

            state.AddModifier(new EntityModifierEntry(poison, new FloatVariableValue { Value = 2f }));
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(7f));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.True);
        }

        [Test]
        public void RuntimeVariable_AfterModifierRemoved_ReadsOriginalBase()
        {
            VariableSO poison = CreateVariableSo("Poison", VariableValueType.Float);
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddVariable(poison, new FloatVariableValue { Value = 5f });
            var mod = new EntityModifierEntry(poison, new FloatVariableValue { Value = 2f });
            state.AddModifier(mod);
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(7f));

            Assert.That(state.RemoveModifier(mod), Is.True);
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(5f));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.False);
            Assert.That(state.InstanceBagHasLocalKey(poison), Is.True);
        }

        [Test]
        public void RemoveVariable_ClearsModifiersAndStructuralSubscriptionFires()
        {
            VariableSO poison = CreateVariableSo("Poison", VariableValueType.Float);
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddVariable(poison, new FloatVariableValue { Value = 5f });
            state.AddModifier(new EntityModifierEntry(poison, new FloatVariableValue { Value = 1f }));

            var removed = new List<Variable>();
            using (state.SubscribeToVariableRemoved(removed.Add))
            {
                Assert.That(state.RemoveVariable(poison), Is.True);
            }

            Assert.That(removed.Count, Is.EqualTo(1));
            Assert.That(removed[0], Is.EqualTo((Variable)poison));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.False);
        }

        [Test]
        public void SubscribeToVariableAdded_FiresWhenRuntimeSlotAdded()
        {
            VariableSO poison = CreateVariableSo("Poison", VariableValueType.Float);
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Variable seen = null!;
            using (state.SubscribeToVariableAdded((k, _) => seen = k))
            {
                state.AddVariable(poison, new FloatVariableValue { Value = 1f });
            }

            Assert.That(seen, Is.EqualTo((Variable)poison));
        }

        [Test]
        public void AddModifier_TypedExtension_Float_SumsCorrectly()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(hp, 5f);
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(15f));
        }

        [Test]
        public void AddModifier_TypedExtension_Int_SumsCorrectly()
        {
            VariableSO n = CreateVariableSo("N", VariableValueType.Int);
            EntityDefinition def = CreateDefinition((n, new IntVariableValue { Value = 10 }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(n, 3);
            Assert.That(state.GetValue<int>(n), Is.EqualTo(13));
        }

        [Test]
        public void AddModifier_ByName_ResolvesAndApplies()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier("HP", 4f);
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(14f));
        }

        [Test]
        public void AddModifier_ByName_UnknownName_Throws()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            Assert.Throws<InvalidOperationException>(() => state.AddModifier("Missing", 1f));
        }

        [Test]
        public void AddVariable_TypedExtension_AddsSlotCorrectly()
        {
            VariableSO poison = CreateVariableSo("Poison", VariableValueType.Float);
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);
            Assert.That(state.AddVariable(poison, 2.5f), Is.True);
            Assert.That(state.GetValue<float>(poison), Is.EqualTo(2.5f));
        }

        [Test]
        public void Subscribe_PrimitiveExtension_FiresFloat()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 7f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            float got = 0f;
            using (state.Subscribe(hp, (float v) => got = v))
            {
                Assert.That(got, Is.EqualTo(7f));
            }
        }

        [Test]
        public void Subscribe_VariableValueExtension_FiresFloatVariableValue()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 8f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            FloatVariableValue got = null!;
            using (state.SubscribeToVariable(hp, (FloatVariableValue v) => got = v))
            {
                Assert.That(got, Is.Not.Null);
                Assert.That(got.Value, Is.EqualTo(8f));
            }
        }

        [Test]
        public void NotifyAllEffectiveValues_FiresSubscribersForAllKeys()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(new EntityModifierEntry(hp, new FloatVariableValue { Value = 2f }));

            int calls = 0;
            state.Subscribe(hp, _ => calls++);
            Assert.That(calls, Is.EqualTo(1));

            state.NotifyAllEffectiveValues();
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void NotifyAllEffectiveValues_WithNoSubscribers_DoesNotThrow()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            Assert.DoesNotThrow(() => state.NotifyAllEffectiveValues());
        }

        [Test]
        public void EntityInstance_Subclass_InheritsFullBehavior()
        {
            VariableSO hp = CreateVariableSo("HP", VariableValueType.Float);
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 3f }));
            var state = new TestEntityInstance();
            state.Initialize(new InstanceId(1), def);
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(3f));
            state.AddModifier(hp, 2f);
            Assert.That(state.GetValue<float>(hp), Is.EqualTo(5f));
        }

        private sealed class TestEntityInstance : EntityInstance<EntityDefinition>
        {
        }

        private VariableSO CreateVariableSo(string assetName, VariableValueType valueType)
        {
            var so = ScriptableObject.CreateInstance<VariableSO>();
            so.name = assetName;
            so.SetValueType(valueType);
            return so;
        }

        private EntityDefinition CreateDefinition(params (VariableSO so, VariableValue baseValue)[] rows)
        {
            var def = new EntityDefinition();
            foreach ((VariableSO so, VariableValue bv) in rows)
            {
                def.AddEntry(VariableEntry.Create((Variable)so, bv));
            }

            def.RebuildLookup();
            return def;
        }
    }
}

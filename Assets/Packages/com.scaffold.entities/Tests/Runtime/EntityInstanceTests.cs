using System;
using System.Collections.Generic;
using System.Reflection;
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
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 100f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(100f));
        }

        [Test]
        public void TryGetVariable_WhenPresent_ReturnsTypedValue()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 42f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.TryGetVariable(hp, out float value), Is.True);
            Assert.That(value, Is.EqualTo(42f));
        }

        [Test]
        public void TryGetVariable_WhenMissing_ReturnsFalse()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.TryGetVariable(hp, out float _), Is.False);
        }

        [Test]
        public void TryGetVariable_WhenTypeMismatch_ReturnsFalse()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.TryGetVariable(hp, out int _), Is.False);
        }

        [Test]
        public void Modifiers_OnInstance_SumFloatValues()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var mod = new EntityModifierEntry(hp, new FloatAddModifier(5f));
            state.AddModifier(mod);

            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(15f));
        }

        [Test]
        public void Modifiers_DoNotMutateDefinition()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));

            Assert.That(def.TryGetDefaultValue((Variable)hp, out VariableValue baseV), Is.True);
            Assert.That(((FloatVariableValue)baseV).Value, Is.EqualTo(10f));
        }

        [Test]
        public void Creator_AssignsNonEmptyInstanceId()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 1f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.Id.Id, Is.GreaterThan(0));
        }

        [Test]
        public void RemoveModifier_AfterNumericCombine_RestoresDefinitionBase()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var mod = new EntityModifierEntry(hp, new FloatAddModifier(5f));
            ModifierId modId = state.AddModifier(mod);

            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(15f));

            Assert.That(state.RemoveModifier((Variable)hp, modId), Is.True);
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void ClearModifiers_RemovesAllContributionsRestoresDefinitionBase()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(4f)));
            state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(1f)));

            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(15f));

            state.ClearModifiers();
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void RemoveModifier_UnknownEntry_ReturnsFalse()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            Assert.That(state.RemoveModifier((Variable)hp, ModifierId.New()), Is.False);
            Assert.That(state.RemoveModifier((Variable)hp, default), Is.False);
        }

        [Test]
        public void Subscribe_FiresImmediatelyWithCurrentValue()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
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
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            VariableValue received = null!;
            state.Subscribe(hp, v => received = v);

            state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));

            Assert.That(received, Is.Not.Null);
            Assert.That(((FloatVariableValue)received).Value, Is.EqualTo(15f));
        }

        [Test]
        public void Subscribe_FiresOnModifierRemove()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var mod = new EntityModifierEntry(hp, new FloatAddModifier(5f));
            ModifierId modId = state.AddModifier(mod);

            VariableValue received = null!;
            state.Subscribe(hp, v => received = v);

            state.RemoveModifier((Variable)hp, modId);

            Assert.That(received, Is.Not.Null);
            Assert.That(((FloatVariableValue)received).Value, Is.EqualTo(10f));
        }

        [Test]
        public void Subscribe_DoesNotFireAfterUnsubscribe()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            void OnChange(VariableValue v) => callCount++;

            state.Subscribe(hp, OnChange);
            Assert.That(callCount, Is.EqualTo(1));

            state.Unsubscribe(hp, OnChange);
            state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_Float_FiresImmediatelyWithTypedValue()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
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
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            float received = 0f;
            using (state.Subscribe(hp, (float v) => received = v))
            {
                state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));
                Assert.That(received, Is.EqualTo(15f));
            }
        }

        [Test]
        public void SubscribeToVariable_FloatVariableValue_FiresImmediately()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
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
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            IDisposable sub = state.Subscribe(hp, (float _) => callCount++);
            Assert.That(callCount, Is.EqualTo(1));

            sub.Dispose();
            state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_Untyped_ReturnsTokenThatDisposes()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            int callCount = 0;
            IDisposable sub = state.Subscribe(hp, _ => callCount++);
            Assert.That(callCount, Is.EqualTo(1));

            sub.Dispose();
            state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void EffectiveBag_NoModifierLayer_ForDefinitionVariableWithoutModifiers()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.ContainsModifiedValueCache(hp), Is.False);
            Assert.That(state.InstanceBagHasLocalKey(hp), Is.False);
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void AddVariable_ThenRead_WorksAndCachesWhenModifiersApplied()
        {
            VariableSO poison = CreateVariableSo("Poison", typeof(FloatVariableValue));
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Assert.That(state.AddVariable(poison, new FloatVariableValue { Value = 5f }), Is.True);
            Assert.That(state.GetVariable<float>(poison), Is.EqualTo(5f));
            Assert.That(state.InstanceBagHasLocalKey(poison), Is.True);

            state.AddModifier(new EntityModifierEntry(poison, new FloatAddModifier(2f)));
            Assert.That(state.GetVariable<float>(poison), Is.EqualTo(7f));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.True);
        }

        [Test]
        public void RuntimeVariable_AfterModifierRemoved_ReadsOriginalBase()
        {
            VariableSO poison = CreateVariableSo("Poison", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddVariable(poison, new FloatVariableValue { Value = 5f });
            var mod = new EntityModifierEntry(poison, new FloatAddModifier(2f));
            ModifierId modId = state.AddModifier(mod);
            Assert.That(state.GetVariable<float>(poison), Is.EqualTo(7f));

            Assert.That(state.RemoveModifier((Variable)poison, modId), Is.True);
            Assert.That(state.GetVariable<float>(poison), Is.EqualTo(5f));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.False);
            Assert.That(state.InstanceBagHasLocalKey(poison), Is.True);
        }

        [Test]
        public void RemoveVariable_ClearsModifiersAndStructuralSubscriptionFires()
        {
            VariableSO poison = CreateVariableSo("Poison", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddVariable(poison, new FloatVariableValue { Value = 5f });
            state.AddModifier(new EntityModifierEntry(poison, new FloatAddModifier(1f)));

            var removed = new List<Variable>();
            using (state.SubscribeToVariableStructuralChanges((kind, k, _) =>
            {
                if (kind == VariableStructuralChange.Removed)
                {
                    removed.Add(k);
                }
            }))
            {
                Assert.That(state.RemoveVariable(poison), Is.True);
            }

            Assert.That(removed.Count, Is.EqualTo(1));
            Assert.That(removed[0], Is.EqualTo((Variable)poison));
            Assert.That(state.ContainsModifiedValueCache(poison), Is.False);
        }

        [Test]
        public void SubscribeToVariableStructuralChanges_FiresWhenRuntimeSlotAdded()
        {
            VariableSO poison = CreateVariableSo("Poison", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);

            Variable seen = null!;
            using (state.SubscribeToVariableStructuralChanges((kind, k, _) =>
            {
                if (kind == VariableStructuralChange.Added)
                {
                    seen = k;
                }
            }))
            {
                state.AddVariable(poison, new FloatVariableValue { Value = 1f });
            }

            Assert.That(seen, Is.EqualTo((Variable)poison));
        }

        [Test]
        public void AddModifier_TypedExtension_Float_SumsCorrectly()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(hp, 5f);
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(15f));
        }

        [Test]
        public void AddModifier_TypedExtension_Int_SumsCorrectly()
        {
            VariableSO n = CreateVariableSo("N", typeof(IntVariableValue));
            EntityDefinition def = CreateDefinition((n, new IntVariableValue { Value = 10 }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(n, 3);
            Assert.That(state.GetVariable<int>(n), Is.EqualTo(13));
        }

        [Test]
        public void AddModifier_ByNameAndType_DelegatesToVariableKey()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier("HP", "float", 4f);
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(14f));
        }

        [Test]
        public void AddModifier_ByNameAndType_UnknownSlot_DoesNotThrow_ModifierOrphaned()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            Assert.DoesNotThrow(() => state.AddModifier("Missing", "float", 1f));
            var orphanKey = new Variable("Missing", "float");
            Assert.Throws<InvalidOperationException>(() => state.GetVariable<float>(orphanKey));
        }

        [Test]
        public void AddVariable_TypedExtension_AddsSlotCorrectly()
        {
            VariableSO poison = CreateVariableSo("Poison", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition();
            EntityInstance<EntityDefinition> state = creator.Create(def);
            Assert.That(state.AddVariable(poison, 2.5f), Is.True);
            Assert.That(state.GetVariable<float>(poison), Is.EqualTo(2.5f));
        }

        [Test]
        public void Subscribe_PrimitiveExtension_FiresFloat()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
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
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
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
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(2f)));

            int calls = 0;
            state.Subscribe(hp, _ => calls++);
            Assert.That(calls, Is.EqualTo(1));

            state.NotifyAllEffectiveValues();
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void NotifyAllEffectiveValues_WithNoSubscribers_DoesNotThrow()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 10f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            Assert.DoesNotThrow(() => state.NotifyAllEffectiveValues());
        }

        [Test]
        public void Modifiers_Order_AddThenMultiply_AppliesMultiplyAfterAdd()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 2f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var add = new FloatAddModifier(3f);
            var mul = new FloatMultiplyModifier(4f);
            SetModifierOrder(mul, 100);
            state.AddModifier(new EntityModifierEntry(hp, add));
            state.AddModifier(new EntityModifierEntry(hp, mul));
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(20f));
        }

        [Test]
        public void Modifiers_Order_MultiplyThenAdd_AppliesAddAfterMultiply()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 2f }));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            var mul = new FloatMultiplyModifier(4f);
            var add = new FloatAddModifier(3f);
            SetModifierOrder(add, 100);
            state.AddModifier(new EntityModifierEntry(hp, mul));
            state.AddModifier(new EntityModifierEntry(hp, add));
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(11f));
        }

        [Test]
        public void Modifiers_StableTie_InsertionOrder_StringAppend()
        {
            VariableSO s = CreateVariableSo("S", typeof(StringVariableValue));
            EntityDefinition def = CreateDefinition((s, new StringVariableValue("a")));
            EntityInstance<EntityDefinition> state = creator.Create(def);
            state.AddModifier(new EntityModifierEntry(s, new StringAppendModifier("b")));
            state.AddModifier(new EntityModifierEntry(s, new StringAppendModifier("c")));
            Assert.That(state.GetVariable<string>(s), Is.EqualTo("abc"));
        }

        [Test]
        public void EntityModifierEntry_Rebase_ReplacesMismatchedValueTypeModifier()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            var entry = new EntityModifierEntry((Variable)hp, new BoolOverrideModifier(true));
            entry.RebaseSerializedModifierPayloadIfMismatch();
            Assert.That(entry.Modifier, Is.TypeOf<FloatAddModifier>());
        }

        [Test]
        public void EntityInstance_Subclass_InheritsFullBehavior()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            EntityDefinition def = CreateDefinition((hp, new FloatVariableValue { Value = 3f }));
            var state = new TestEntityInstance();
            state.Initialize(new InstanceId(1), def);
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(3f));
            state.AddModifier(hp, 2f);
            Assert.That(state.GetVariable<float>(hp), Is.EqualTo(5f));
        }

        private sealed class TestEntityInstance : EntityInstance<EntityDefinition>
        {
        }

        private static void SetModifierOrder(VariableModifier modifier, int order)
        {
            FieldInfo? field = typeof(VariableModifier).GetField("order", BindingFlags.Instance | BindingFlags.NonPublic);
            field!.SetValue(modifier, order);
        }

        private VariableSO CreateVariableSo(string assetName, Type payloadType)
        {
            var so = ScriptableObject.CreateInstance<VariableSO>();
            so.name = assetName;
            so.SetPayloadType(payloadType);
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

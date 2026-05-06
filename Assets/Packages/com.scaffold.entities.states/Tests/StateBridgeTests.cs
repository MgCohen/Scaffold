using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scaffold.Entities;
using Scaffold.Entities.States;
using Scaffold.States;

namespace Scaffold.Entities.States.Tests
{
    public sealed class StateBridgeTests
    {
        private static Variable Var(string name) => new Variable(name, "int");
        private static IntVariableValue Int(int v) => new IntVariableValue(v);

        private static Store NewStoreWithBridge()
        {
            var builder = new StoreBuilder();
            EntityBridgeContext.RegisterMutators(builder);
            return builder.Build();
        }

        private static EntityDefinition Def(Variable key, int value)
        {
            var def = new EntityDefinition();
            def.AddVariable(key, Int(value));
            return def;
        }

        private static IntAddModifier ModWithOrder(int amount, int order)
        {
            var mod = new IntAddModifier(amount);
            typeof(VariableModifier)
                .GetField("order", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mod, order);
            return mod;
        }

        private static ActiveModifier MakeMod(int amount, int order = 0)
        {
            return new ActiveModifier(ModifierId.New(), ModWithOrder(amount, order), null);
        }

        // --- Bridge integration tests (migrated to StoreEntities.Spawn) ---

        [Test]
        public void Spawn_DefaultRead_ReturnsDefinitionDefault()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var (entity, _) = store.Spawn(Def(hp, 10));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(10));
        }

        [Test]
        public void SetBaseValue_ThenRead_ReturnsBase()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var (entity, _) = store.Spawn(Def(hp, 10));

            entity.SetBaseValue(hp, Int(7));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(7));
        }

        [Test]
        public void AddModifier_AppliesToBase()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var (entity, _) = store.Spawn(Def(hp, 10));

            entity.SetBaseValue(hp, Int(5));
            entity.AddModifier(hp, new IntAddModifier(3));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(8));
        }

        [Test]
        public void RemoveModifier_RestoresBase()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var (entity, _) = store.Spawn(Def(hp, 10));
            ModifierId id = entity.AddModifier(hp, new IntAddModifier(5));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(15));
            entity.RemoveModifier(hp, id);
            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(10));
        }

        // --- EntityState.TryGetBase ---

        [Test]
        public void EntityState_TryGetBase_ReturnsValue()
        {
            var k = Var("hp");
            var v = Int(42);
            var state = EntityState.Empty.WithBaseValue(k, v);

            Assert.That(state.TryGetBase(k, out var result), Is.True);
            Assert.That(result, Is.EqualTo(v));
        }

        [Test]
        public void EntityState_TryGetBase_FalseWhenAbsent()
        {
            Assert.That(EntityState.Empty.TryGetBase(Var("hp"), out _), Is.False);
        }

        // --- EntityState.GetModifiers ---

        [Test]
        public void EntityState_GetModifiers_ReturnsPreSorted()
        {
            var k = Var("hp");
            var m1 = MakeMod(1, order: 10);
            var m2 = MakeMod(2, order: 5);
            var m3 = MakeMod(3, order: 10);

            var state = EntityState.Empty
                .WithModifier(k, m1)
                .WithModifier(k, m2)
                .WithModifier(k, m3);

            var result = state.GetModifiers(k).ToList();
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0].Id, Is.EqualTo(m2.Id));
            Assert.That(result[1].Id, Is.EqualTo(m1.Id));
            Assert.That(result[2].Id, Is.EqualTo(m3.Id));
        }

        [Test]
        public void EntityState_GetModifiers_EmptyWhenAbsent()
        {
            var result = EntityState.Empty.GetModifiers(Var("hp"));
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        // --- EntityState.Variables ---

        [Test]
        public void EntityState_Variables_UnionsBasesAndModifiers()
        {
            var baseOnly = Var("base_only");
            var modOnly = Var("mod_only");
            var both = Var("both");

            var state = EntityState.Empty
                .WithBaseValue(baseOnly, Int(1))
                .WithBaseValue(both, Int(2))
                .WithModifier(modOnly, MakeMod(1))
                .WithModifier(both, MakeMod(2));

            var vars = new HashSet<Variable>(state.Variables);
            Assert.That(vars, Has.Member(baseOnly));
            Assert.That(vars, Has.Member(modOnly));
            Assert.That(vars, Has.Member(both));
        }

        // --- EntityState.WithoutAllModifiers ---

        [Test]
        public void EntityState_WithoutAllModifiers_ClearsEverything()
        {
            var state = EntityState.Empty
                .WithModifier(Var("a"), MakeMod(1))
                .WithModifier(Var("b"), MakeMod(2))
                .WithoutAllModifiers();

            Assert.That(state.ModifierStacks, Is.Empty);
        }

        [Test]
        public void EntityState_WithoutAllModifiers_PreservesBases()
        {
            var k = Var("hp");
            var v = Int(10);
            var state = EntityState.Empty
                .WithBaseValue(k, v)
                .WithModifier(k, MakeMod(3))
                .WithoutAllModifiers();

            Assert.That(state.TryGetBase(k, out var result), Is.True);
            Assert.That(result, Is.EqualTo(v));
        }

        // --- EntityState.WithoutBase ---

        [Test]
        public void EntityState_WithoutBase_RemovesBaseOnly()
        {
            var k = Var("hp");
            var state = EntityState.Empty
                .WithBaseValue(k, Int(10))
                .WithModifier(k, MakeMod(5))
                .WithoutBase(k);

            Assert.That(state.TryGetBase(k, out _), Is.False);
            Assert.That(state.GetModifiers(k).Count(), Is.EqualTo(1));
        }

        [Test]
        public void EntityState_WithoutBase_NoOpWhenAbsent()
        {
            var state = EntityState.Empty;
            var result = state.WithoutBase(Var("hp"));
            Assert.That(result, Is.SameAs(state));
        }

        // --- Removal verification ---

        [Test]
        public void EntityState_ResolveEffectiveValues_Removed()
        {
            var method = typeof(EntityState).GetMethod("ResolveEffectiveValues");
            Assert.That(method, Is.Null);
        }

        // --- ClearModifiersPayload ---

        [Test]
        public void ClearModifiersPayload_Exists()
        {
            var type = typeof(EntityState).Assembly.GetType("Scaffold.Entities.States.ClearModifiersPayload");
            Assert.That(type, Is.Not.Null);
        }

        [Test]
        public void ClearModifiersPayload_GetReference()
        {
            var r = new Ref<object>(Guid.NewGuid());
            var payload = new ClearModifiersPayload(r);
            Assert.That(payload.GetReference(), Is.EqualTo(r));
        }

        // --- ClearModifiersMutator (tested via store dispatch; mutator is internal) ---

        [Test]
        public void ClearModifiersMutator_DelegatesToWithoutAllModifiers()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var (entity, entityRef) = store.Spawn(Def(hp, 10));

            entity.AddModifier(hp, new IntAddModifier(5));
            Assert.That(store.Get<EntityState>(entityRef).ModifierStacks, Is.Not.Empty);

            store.Execute(new ClearModifiersPayload(entityRef));
            Assert.That(store.Get<EntityState>(entityRef).ModifierStacks, Is.Empty);
        }

        // --- Dispatcher routes ClearModifiersPayload ---

        [Test]
        public void Dispatcher_RoutesClearModifiersPayload()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var atk = Var("atk");
            var (entity, entityRef) = store.Spawn(Def(hp, 10));

            entity.AddModifier(hp, new IntAddModifier(1));
            entity.AddModifier(atk, new IntAddModifier(2));

            store.Execute(new ClearModifiersPayload(entityRef));

            Assert.That(store.Get<EntityState>(entityRef).ModifierStacks, Is.Empty);
        }

        // --- StoreVariableStorage delegation ---

        [Test]
        public void StoreVariableStorage_ClearModifiers_SingleDispatch()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var atk = Var("atk");
            var (entity, entityRef) = store.Spawn(Def(hp, 10));

            entity.SetBaseValue(hp, Int(5));
            entity.AddModifier(hp, new IntAddModifier(1));
            entity.AddModifier(atk, new IntAddModifier(2));

            entity.ClearModifiers();

            var slice = store.Get<EntityState>(entityRef);
            Assert.That(slice.ModifierStacks, Is.Empty);
            Assert.That(slice.TryGetBase(hp, out _), Is.True);
        }

        [Test]
        public void StoreVariableStorage_TryGetBase_DelegatesToSliceMethod()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var (entity, entityRef) = store.Spawn(Def(hp, 10));

            entity.SetBaseValue(hp, Int(7));

            Assert.That(store.Get<EntityState>(entityRef).TryGetBase(hp, out var sliceVal), Is.True);
            Assert.That(entity.Storage.TryGetBase(hp, out var storageVal), Is.True);
            Assert.That(storageVal, Is.EqualTo(sliceVal));
        }

        [Test]
        public void StoreVariableStorage_GetModifiers_DelegatesToSliceMethod()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var (entity, entityRef) = store.Spawn(Def(hp, 10));

            entity.AddModifier(hp, new IntAddModifier(3));

            var sliceMods = store.Get<EntityState>(entityRef).GetModifiers(hp).ToList();
            var storageMods = entity.Storage.GetModifiers(hp).ToList();
            Assert.That(storageMods.Count, Is.EqualTo(sliceMods.Count));
        }

        [Test]
        public void StoreVariableStorage_Variables_DelegatesToSliceMethod()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var atk = Var("atk");
            var (entity, entityRef) = store.Spawn(Def(hp, 10));

            entity.SetBaseValue(hp, Int(5));
            entity.AddModifier(atk, new IntAddModifier(1));

            var sliceVars = new HashSet<Variable>(store.Get<EntityState>(entityRef).Variables);
            var storageVars = new HashSet<Variable>(entity.Storage.Variables);
            Assert.That(storageVars.SetEquals(sliceVars), Is.True);
        }

        // --- StoreEntities.Spawn ---

        [Test]
        public void StoreEntities_Spawn_ReturnsHandleAndRef()
        {
            var store = NewStoreWithBridge();
            var (entity, entityRef) = store.Spawn(Def(Var("hp"), 10));

            Assert.That(entity, Is.Not.Null);
            Assert.That(entityRef, Is.Not.EqualTo(default(Ref<EntityInstance<EntityDefinition>>)));
        }

        [Test]
        public void StoreEntities_Spawn_RegistersInCatalog()
        {
            var store = NewStoreWithBridge();
            var (entity, entityRef) = store.Spawn(Def(Var("hp"), 10));

            var resolved = store.Catalog.Resolve(entityRef);
            Assert.That(resolved, Is.SameAs(entity));
        }

        [Test]
        public void StoreEntities_Spawn_RegistersEmptySlice()
        {
            var store = NewStoreWithBridge();
            var (_, entityRef) = store.Spawn(Def(Var("hp"), 10));

            var slice = store.Get<EntityState>(entityRef);
            Assert.That(slice, Is.EqualTo(EntityState.Empty));
        }

        [Test]
        public void StoreEntities_Spawn_StorageIsStoreBacked()
        {
            var store = NewStoreWithBridge();
            var (entity, _) = store.Spawn(Def(Var("hp"), 10));

            Assert.That(entity.Storage, Is.InstanceOf<StoreVariableStorage>());
            Assert.That(entity.Storage.Parent, Is.Null);
        }

        // --- Deletion verification ---

        [Test]
        public void EntityStateFactory_TypeNotPresent()
        {
            var type = typeof(EntityState).Assembly.GetType("Scaffold.Entities.States.EntityStateFactory");
            Assert.That(type, Is.Null);
        }
    }
}

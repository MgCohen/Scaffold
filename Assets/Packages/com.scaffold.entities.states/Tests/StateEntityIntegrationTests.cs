using System;
using System.Collections.Generic;

using NUnit.Framework;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States.Tests
{
    public class StateEntityIntegrationTests
    {
        private static readonly Variable hp = new("hp", "float");

        [Test]
        public void AddModifier_ChangesEffectiveValue()
        {
            var (store, _, entity, id) = CreateEntity();
            var addMod = new FloatAddModifier(5f);
            var modId = ModifierId.New();
            store.Execute(id, new AddModifierPayload(id, hp, addMod, modId));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));
        }

        [Test]
        public void GetVariable_ReturnsDefinitionDefault_WhenNoOverridesOrModifiers()
        {
            var (_, _, entity, _) = CreateEntity();
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void Modifier_OrderIsRespected_AddBeforeMultiply()
        {
            var (store, _, entity, id) = CreateEntity();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(3f), ModifierId.New()));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatMultiplyModifier(4f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(52f));
        }

        [Test]
        public void RemoveModifier_ByModifierId_RestoresPriorValue()
        {
            var (store, _, entity, id) = CreateEntity();
            var modId = ModifierId.New();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), modId));
            store.Execute(id, new RemoveModifierPayload(id, hp, modId));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void Snapshot_RoundTripsModifierStack()
        {
            var (store, _, entity, id) = CreateEntity();
            Snapshot snapshot = store.SaveSnapshot();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));
            store.LoadSnapshot(snapshot);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void StateEntity_IsAssignableToReadAndMutableEntity()
        {
            Assert.That(typeof(IReadOnlyEntity<EntityDefinition>).IsAssignableFrom(typeof(StateEntity<EntityDefinition>)), Is.True);
            Assert.That(typeof(IMutableEntity<EntityDefinition>).IsAssignableFrom(typeof(StateEntity<EntityDefinition>)), Is.True);
        }

        [Test]
        public void EntityStateFactory_AssignsToReadAndMutableHandles()
        {
            var store = new StoreBuilder().Build();
            EntityBridgeContext.RegisterMutators(store);
            var def = new EntityDefinition();
            def.AddVariable(hp, new FloatVariableValue(0f));
            StateEntity<EntityDefinition> entity = EntityStateFactory.Create(def, store, new InstanceId(1));
            IReadOnlyEntity<EntityDefinition> readHandle = entity;
            IMutableEntity<EntityDefinition> mutableHandle = entity;
            Assert.That(readHandle.Id, Is.EqualTo(mutableHandle.Id));
        }

        [Test]
        public void EntityMutation_RoutesThroughStore_AndProducesSameStateAsDirectExecute()
        {
            var (storeA, _, entityA, idA) = CreateEntity();
            var (storeB, _, _, idB) = CreateEntity();
            entityA.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));
            storeB.Execute(idB, new AddModifierPayload(idB, hp, new FloatAddModifier(5f), ModifierId.New()));
            EntityVariableState stateA = storeA.Get<EntityVariableState>(idA);
            EntityVariableState stateB = storeB.Get<EntityVariableState>(idB);
            Assert.That(stateA.ModifierStacks.ContainsKey(hp), Is.True);
            Assert.That(stateB.ModifierStacks.ContainsKey(hp), Is.True);
            Assert.That(stateA.ModifierStacks[hp].Count, Is.EqualTo(stateB.ModifierStacks[hp].Count));
            Assert.That(entityA.GetVariable<float>(hp), Is.EqualTo(15f));
        }

        [Test]
        public void SetBaseValue_UpdatesBaseAndRebuildsEffective()
        {
            var (store, _, entity, id) = CreateEntity();
            store.Execute(id, new SetBaseValuePayload(id, hp, new FloatVariableValue(20f)));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(20f));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(25f));
        }

        [Test]
        public void AddEntityVariable_AddsRuntimeVariableThatWasNotInDefinition()
        {
            var (store, _, entity, id) = CreateEntity();
            var armor = new Variable("armor", "float");
            store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(7f)));
            Assert.That(entity.GetVariable<float>(armor), Is.EqualTo(7f));
            store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(99f)));
            Assert.That(entity.GetVariable<float>(armor), Is.EqualTo(7f));
        }

        [Test]
        public void EntitySubscription_FiresOnEffectiveChange()
        {
            var (store, _, entity, id) = CreateEntity();
            var captured = new List<float>();
            entity.Subscribe(hp, value => captured.Add(((IVariableValue<float>)value).Get()));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(2f), ModifierId.New()));
            Assert.That(captured.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(captured[captured.Count - 1], Is.EqualTo(17f));
        }

        [Test]
        public void Snapshot_ContainsOnlyCanonicalEntityVariableState()
        {
            var (store, _, entity, id) = CreateEntity();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Snapshot snapshot = store.SaveSnapshot();
            Assert.That(snapshot.Contains(EntityStateReference.From(id), typeof(EntityVariableState)), Is.True);
            Assert.That(snapshot.Count, Is.EqualTo(1));
        }

        [Test]
        public void TwoEntities_SnapshotRoundTrip_RestoresBothEntities()
        {
            var heroDef = new EntityDefinition();
            heroDef.AddVariable(hp, new FloatVariableValue(10f));
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var store = new StoreBuilder().Build();
            EntityBridgeContext.RegisterMutators(store);
            var heroId = new InstanceId(1);
            var goblinId = new InstanceId(2);
            StateEntity<EntityDefinition> hero = EntityStateFactory.Create(heroDef, store, heroId);
            StateEntity<EntityDefinition> goblin = EntityStateFactory.Create(goblinDef, store, goblinId);
            Snapshot snapshot = store.SaveSnapshot();
            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New()));
            store.Execute(goblinId, new AddModifierPayload(goblinId, hp, new FloatAddModifier(3f), ModifierId.New()));
            Assert.That(hero.GetVariable<float>(hp), Is.EqualTo(15f));
            Assert.That(goblin.GetVariable<float>(hp), Is.EqualTo(33f));
            store.LoadSnapshot(snapshot);
            Assert.That(hero.GetVariable<float>(hp), Is.EqualTo(10f));
            Assert.That(goblin.GetVariable<float>(hp), Is.EqualTo(30f));
        }

        [Test]
        public void TwoEntities_AddModifierAppliesOnceToTargetOnly()
        {
            var heroDef = new EntityDefinition();
            heroDef.AddVariable(hp, new FloatVariableValue(10f));
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var store = new StoreBuilder().Build();
            EntityBridgeContext.RegisterMutators(store);
            var heroId = new InstanceId(1);
            var goblinId = new InstanceId(2);
            StateEntity<EntityDefinition> hero = EntityStateFactory.Create(heroDef, store, heroId);
            StateEntity<EntityDefinition> goblin = EntityStateFactory.Create(goblinDef, store, goblinId);
            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(hero.GetVariable<float>(hp), Is.EqualTo(15f));
            Assert.That(goblin.GetVariable<float>(hp), Is.EqualTo(30f));
        }

        [Test]
        public void EntityReads_RevertAfterLoadSnapshot()
        {
            var (store, _, entity, id) = CreateEntity();
            Snapshot snapshot = store.SaveSnapshot();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));
            store.LoadSnapshot(snapshot);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void Snapshots_BackAndForth_RestoreExpectedValues()
        {
            var (store, _, entity, id) = CreateEntity();
            Snapshot snapA = store.SaveSnapshot();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Snapshot snapB = store.SaveSnapshot();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatMultiplyModifier(2f), ModifierId.New()));
            Snapshot snapC = store.SaveSnapshot();
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(30f));
            store.LoadSnapshot(snapA);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
            store.LoadSnapshot(snapC);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(30f));
            store.LoadSnapshot(snapB);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));
            store.LoadSnapshot(snapA);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(3f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(13f));
        }

        [Test]
        public void ModifierId_SurvivesSnapshotLoadCycle()
        {
            var (store, _, entity, id) = CreateEntity();
            ModifierId modId = ModifierId.New();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), modId));
            Snapshot snapshot = store.SaveSnapshot();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(2f), ModifierId.New()));
            store.LoadSnapshot(snapshot);
            store.Execute(id, new RemoveModifierPayload(id, hp, modId));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void LoadingSameSnapshotTwice_ProducesSameResult()
        {
            var (store, _, entity, id) = CreateEntity();
            Snapshot snapshotAtBase = store.SaveSnapshot();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            store.LoadSnapshot(snapshotAtBase);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(7f), ModifierId.New()));
            _ = store.SaveSnapshot();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(99f), ModifierId.New()));
            store.LoadSnapshot(snapshotAtBase);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void TwoEntities_DistinctMidSnapshotStates_RestoreIndependently()
        {
            var heroDef = new EntityDefinition();
            heroDef.AddVariable(hp, new FloatVariableValue(10f));
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var store = new StoreBuilder().Build();
            EntityBridgeContext.RegisterMutators(store);
            var heroId = new InstanceId(1);
            var goblinId = new InstanceId(2);
            StateEntity<EntityDefinition> hero = EntityStateFactory.Create(heroDef, store, heroId);
            StateEntity<EntityDefinition> goblin = EntityStateFactory.Create(goblinDef, store, goblinId);
            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New()));
            store.Execute(goblinId, new AddModifierPayload(goblinId, hp, new FloatAddModifier(3f), ModifierId.New()));
            Snapshot snapshot = store.SaveSnapshot();
            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatMultiplyModifier(2f), ModifierId.New()));
            store.Execute(goblinId, new AddModifierPayload(goblinId, hp, new FloatAddModifier(10f), ModifierId.New()));
            store.LoadSnapshot(snapshot);
            Assert.That(hero.GetVariable<float>(hp), Is.EqualTo(15f));
            Assert.That(goblin.GetVariable<float>(hp), Is.EqualTo(33f));
        }

        [Test]
        public void LoadSnapshot_PrunesEntitiesCreatedAfterSnapshot()
        {
            var (store, _, heroEntity, heroId) = CreateEntity();
            Snapshot snapshot = store.SaveSnapshot();
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var goblinId = new InstanceId(2);
            StateEntity<EntityDefinition> goblinEntity = EntityStateFactory.Create(goblinDef, store, goblinId);
            Assert.That(goblinEntity.GetVariable<float>(hp), Is.EqualTo(30f));
            store.LoadSnapshot(snapshot);
            Assert.That(heroEntity.GetVariable<float>(hp), Is.EqualTo(10f));
            Assert.Throws<KeyNotFoundException>(() => _ = store.Get<EntityVariableState>(goblinId));
            Assert.Throws<KeyNotFoundException>(() => _ = goblinEntity.GetVariable<float>(hp));
        }

        [Test]
        public void EntitySubscription_FiresAfterLoadSnapshot()
        {
            var (store, _, entity, id) = CreateEntity();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Snapshot snapshot = store.SaveSnapshot();
            var captured = new List<float>();
            entity.Subscribe(hp, value => captured.Add(((IVariableValue<float>)value).Get()));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(2f), ModifierId.New()));
            int captureCountAfterMutation = captured.Count;
            store.LoadSnapshot(snapshot);
            Assert.That(captured.Count, Is.GreaterThan(captureCountAfterMutation));
            Assert.That(captured[captured.Count - 1], Is.EqualTo(15f));
        }

        [Test]
        public void MixedPayloads_SurviveSnapshotRoundTrip()
        {
            var (store, _, entity, id) = CreateEntity();
            var armor = new Variable("armor", "float");
            store.Execute(id, new SetBaseValuePayload(id, hp, new FloatVariableValue(20f)));
            ModifierId hpModId = ModifierId.New();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), hpModId));
            store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(7f)));
            Snapshot snapshot = store.SaveSnapshot();
            store.Execute(id, new SetBaseValuePayload(id, hp, new FloatVariableValue(99f)));
            store.Execute(id, new RemoveModifierPayload(id, hp, hpModId));
            store.Execute(id, new SetBaseValuePayload(id, armor, new FloatVariableValue(99f)));
            float preLoadArmor = entity.GetVariable<float>(armor);
            Assert.That(preLoadArmor, Is.EqualTo(99f));
            store.LoadSnapshot(snapshot);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(25f));
            Assert.That(entity.GetVariable<float>(armor), Is.EqualTo(7f));
        }

        [Test]
        public void StaleEntityReference_AfterPruneCanonical_ThrowsOnRead()
        {
            var (store, _, _, _) = CreateEntity();
            Snapshot snapshot = store.SaveSnapshot();
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var goblinId = new InstanceId(2);
            StateEntity<EntityDefinition> goblinEntity = EntityStateFactory.Create(goblinDef, store, goblinId);
            store.Execute(goblinId, new AddModifierPayload(goblinId, hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(goblinEntity.GetVariable<float>(hp), Is.EqualTo(35f));
            store.LoadSnapshot(snapshot);
            Assert.Throws<KeyNotFoundException>(() => _ = store.Get<EntityVariableState>(goblinId));
            Assert.Throws<KeyNotFoundException>(() => _ = goblinEntity.GetVariable<float>(hp));
        }

        [Test]
        public void ExecuteOnPrunedEntity_Throws()
        {
            var (store, _, _, _) = CreateEntity();
            Snapshot snapshot = store.SaveSnapshot();
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var goblinId = new InstanceId(2);
            EntityStateFactory.Create(goblinDef, store, goblinId);
            store.LoadSnapshot(snapshot);
            Assert.Throws<KeyNotFoundException>(
                () => store.Execute(
                    goblinId,
                    new AddModifierPayload(goblinId, hp, new FloatAddModifier(5f), ModifierId.New())));
        }

        [Test]
        public void TwoEntities_ResolveTheirOwnDefaults()
        {
            var heroDef = new EntityDefinition();
            heroDef.AddVariable(hp, new FloatVariableValue(10f));
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var store = new StoreBuilder().Build();
            EntityBridgeContext.RegisterMutators(store);
            StateEntity<EntityDefinition> hero = EntityStateFactory.Create(heroDef, store, new InstanceId(1));
            StateEntity<EntityDefinition> goblin = EntityStateFactory.Create(goblinDef, store, new InstanceId(2));
            Assert.That(hero.GetVariable<float>(hp), Is.EqualTo(10f));
            Assert.That(goblin.GetVariable<float>(hp), Is.EqualTo(30f));
        }

        [Test]
        public void RemoveVariable_ClearsBaseAndModifiers_AndFallsBackToDefinitionDefault()
        {
            var (store, _, entity, id) = CreateEntity();
            var armor = new Variable("armor", "float");
            store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(7f)));
            store.Execute(id, new AddModifierPayload(id, armor, new FloatAddModifier(3f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(armor), Is.EqualTo(10f));
            bool removed = entity.RemoveVariable(armor);
            Assert.That(removed, Is.True);
            Assert.That(entity.TryGetVariable<float>(armor, out _), Is.False);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void RemoveVariable_OnDefinitionVariable_ClearsRuntimeOverridesButLeavesDefinitionDefault()
        {
            var (store, _, entity, id) = CreateEntity();
            store.Execute(id, new SetBaseValuePayload(id, hp, new FloatVariableValue(20f)));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(25f));
            bool removed = entity.RemoveVariable(hp);
            Assert.That(removed, Is.True);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void RemoveVariable_OnUnknownVariable_ReturnsFalse()
        {
            var (_, _, entity, _) = CreateEntity();
            var unknown = new Variable("unknown", "float");
            Assert.That(entity.RemoveVariable(unknown), Is.False);
        }

        [Test]
        public void LoadSnapshot_RestoresUnregisteredCanonicalSlice_EntityReadsAgain()
        {
            var (store, _, entity, id) = CreateEntity();
            Snapshot snapshot = store.SaveSnapshot();
            Assert.That(store.UnregisterSlice<EntityVariableState>(id), Is.True);
            Assert.Throws<KeyNotFoundException>(() => _ = store.Get<EntityVariableState>(id));
            store.LoadSnapshot(snapshot);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void ClearModifiers_ExecuteBatch_RoutesEachPayloadByEntityId()
        {
            var (store, _, entity, id) = CreateEntity();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(4f), ModifierId.New()));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(3f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(17f));
            entity.ClearModifiers();
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void AddModifierWithSource_StoresSourceInActiveModifier()
        {
            var (store, _, _, id) = CreateEntity();
            var src = new ModifierSource(new InstanceId(99));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New(), src));
            EntityVariableState state = store.Get<EntityVariableState>(id);
            IReadOnlyList<ActiveModifier> stack = state.ModifierStacks[hp];
            Assert.That(stack.Count, Is.EqualTo(1));
            Assert.That(stack[0].Source, Is.EqualTo(src));
        }

        [Test]
        public void RemoveModifiersBySource_OnEntity_ClearsOnlyMatchingSource()
        {
            var (store, _, entity, id) = CreateEntity();
            var srcA = new ModifierSource(new InstanceId(101));
            var srcB = new ModifierSource(new InstanceId(102));

            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New(), srcA));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(7f), ModifierId.New(), srcB));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(2f), ModifierId.New()));

            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f + 5f + 7f + 2f));

            store.Execute(id, new RemoveModifiersBySourcePayload(id, srcA));

            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f + 7f + 2f));
        }

        [Test]
        public void RemoveModifiersFromSource_GlobalSweep_ClearsAcrossEveryEntity()
        {
            var heroDef = new EntityDefinition();
            heroDef.AddVariable(hp, new FloatVariableValue(10f));
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));

            var store = new StoreBuilder().Build();
            EntityBridgeContext.RegisterMutators(store);

            var heroId = new InstanceId(1);
            var goblinId = new InstanceId(2);
            StateEntity<EntityDefinition> hero = EntityStateFactory.Create(heroDef, store, heroId);
            StateEntity<EntityDefinition> goblin = EntityStateFactory.Create(goblinDef, store, goblinId);

            var auraSource = new ModifierSource(new InstanceId(999));
            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New(), auraSource));
            store.Execute(goblinId, new AddModifierPayload(goblinId, hp, new FloatAddModifier(3f), ModifierId.New(), auraSource));
            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(2f), ModifierId.New()));

            Assert.That(hero.GetVariable<float>(hp), Is.EqualTo(17f));
            Assert.That(goblin.GetVariable<float>(hp), Is.EqualTo(33f));

            StateEntityOps.RemoveModifiersFromSource(store, auraSource);

            Assert.That(hero.GetVariable<float>(hp), Is.EqualTo(12f));
            Assert.That(goblin.GetVariable<float>(hp), Is.EqualTo(30f));
        }

        [Test]
        public void RemoveModifiersFromSource_GlobalSweep_FiresOneUpdatedPerAffectedEntity()
        {
            var heroDef = new EntityDefinition();
            heroDef.AddVariable(hp, new FloatVariableValue(10f));
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var elfDef = new EntityDefinition();
            elfDef.AddVariable(hp, new FloatVariableValue(50f));

            var store = new StoreBuilder().Build();
            EntityBridgeContext.RegisterMutators(store);

            var heroId = new InstanceId(1);
            var goblinId = new InstanceId(2);
            var elfId = new InstanceId(3);
            EntityStateFactory.Create(heroDef, store, heroId);
            EntityStateFactory.Create(goblinDef, store, goblinId);
            EntityStateFactory.Create(elfDef, store, elfId);

            var auraSource = new ModifierSource(new InstanceId(999));
            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New(), auraSource));
            store.Execute(goblinId, new AddModifierPayload(goblinId, hp, new FloatAddModifier(3f), ModifierId.New(), auraSource));

            int heroUpdates = 0;
            int goblinUpdates = 0;
            int elfUpdates = 0;
            store.Subscribe<EntityVariableState>(heroId, (_, _, ev) => { if (ev == StateChangeEvent.Updated) { heroUpdates++; } });
            store.Subscribe<EntityVariableState>(goblinId, (_, _, ev) => { if (ev == StateChangeEvent.Updated) { goblinUpdates++; } });
            store.Subscribe<EntityVariableState>(elfId, (_, _, ev) => { if (ev == StateChangeEvent.Updated) { elfUpdates++; } });

            StateEntityOps.RemoveModifiersFromSource(store, auraSource);

            Assert.That(heroUpdates, Is.EqualTo(1));
            Assert.That(goblinUpdates, Is.EqualTo(1));
            Assert.That(elfUpdates, Is.EqualTo(0));
        }

        [Test]
        public void ExistingRemoveModifierPayload_IsAdditive_NotBroken()
        {
            var (store, _, entity, id) = CreateEntity();
            ModifierId modId = ModifierId.New();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), modId, new ModifierSource(new InstanceId(50))));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));
            store.Execute(id, new RemoveModifierPayload(id, hp, modId));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void OnEntityRemoved_FiresWhenCanonicalSliceIsUnregistered()
        {
            var (store, _, entity, id) = CreateEntity();
            bool fired = false;
            entity.OnEntityRemoved += () => fired = true;
            Assert.That(store.UnregisterSlice<EntityVariableState>(id), Is.True);
            Assert.That(fired, Is.True);
        }

        [Test]
        public void OnEntityRemoved_FiresExactlyOnceWhenLoadSnapshot_PrunesEntity()
        {
            var (store, _, _, _) = CreateEntity();
            Snapshot snap = store.SaveSnapshot();

            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));
            var goblinId = new InstanceId(2);
            StateEntity<EntityDefinition> goblin = EntityStateFactory.Create(goblinDef, store, goblinId);

            int goblinFireCount = 0;
            goblin.OnEntityRemoved += () => goblinFireCount++;

            store.LoadSnapshot(snap);

            Assert.That(goblinFireCount, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotRoundTrip_EntityMutation_RevertsEffectiveValue()
        {
            var (store, _, entity, id) = CreateEntity();
            Snapshot snapshot = store.SaveSnapshot();
            entity.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));
            store.LoadSnapshot(snapshot);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        private static (Store store, EntityDefinition def, StateEntity<EntityDefinition> entity, InstanceId id) CreateEntity()
        {
            var def = new EntityDefinition();
            def.AddVariable(hp, new FloatVariableValue(10f));
            var builder = new StoreBuilder();
            Store store = builder.Build();
            EntityBridgeContext.RegisterMutators(store);
            var id = new InstanceId(1);
            StateEntity<EntityDefinition> entity = EntityStateFactory.Create(def, store, id);
            return (store, def, entity, id);
        }
    }
}

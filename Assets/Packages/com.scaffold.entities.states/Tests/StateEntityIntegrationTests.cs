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
            var (store, _, _, id) = CreateEntity();
            var addMod = new FloatAddModifier(5f);
            var modId = ModifierId.New();
            var payload = new AddModifierPayload(id, hp, addMod, modId);
            store.Execute(id, payload);
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(15f));
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
            var (store, _, _, id) = CreateEntity();
            var add = new FloatAddModifier(3f);
            var mul = new FloatMultiplyModifier(4f);
            var idA = ModifierId.New();
            var idB = ModifierId.New();
            var addPayload = new AddModifierPayload(id, hp, add, idA);
            var mulPayload = new AddModifierPayload(id, hp, mul, idB);
            store.Execute(id, addPayload);
            store.Execute(id, mulPayload);
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(52f));
        }

        [Test]
        public void RemoveModifier_ByModifierId_RestoresPriorValue()
        {
            var (store, _, _, id) = CreateEntity();
            var addMod = new FloatAddModifier(5f);
            var modId = ModifierId.New();
            var addPayload = new AddModifierPayload(id, hp, addMod, modId);
            store.Execute(id, addPayload);
            var removePayload = new RemoveModifierPayload(id, hp, modId);
            store.Execute(id, removePayload);
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void Snapshot_RoundTripsModifierStack()
        {
            var (store, _, _, id) = CreateEntity();
            var snapshot = store.SaveSnapshot();

            var addMod = new FloatAddModifier(5f);
            var modId = ModifierId.New();
            var payload = new AddModifierPayload(id, hp, addMod, modId);
            store.Execute(id, payload);
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(15f));

            store.LoadSnapshot(snapshot);
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void StateEntity_IsImmutableRecord_NotAssignableToMutableEntity()
        {
            var (_, _, entity, _) = CreateEntity();
            var t = entity.GetType();
            Assert.That(t.IsClass, Is.True, "Records compile to classes.");
            Assert.That(typeof(IMutableEntity<EntityDefinition>).IsAssignableFrom(t), Is.False, "StateEntity must not be assignable to IMutableEntity — writes go through the store.");
            Assert.That(typeof(AggregateState).IsAssignableFrom(t), Is.True, "StateEntity must extend AggregateState so it participates in the aggregate-slice rebuild path.");
        }

        [Test]
        public void SetBaseValue_UpdatesBaseAndRebuildsEffective()
        {
            var (store, _, _, id) = CreateEntity();

            store.Execute(id, new SetBaseValuePayload(id, hp, new FloatVariableValue(20f)));
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(20f));

            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(25f),
                "Modifier should fold over the new base value, not the original definition default.");
        }

        [Test]
        public void AddEntityVariable_AddsRuntimeVariableThatWasNotInDefinition()
        {
            var (store, _, _, id) = CreateEntity();
            var armor = new Variable("armor", "float");

            store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(7f)));
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(armor), Is.EqualTo(7f));

            store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(99f)));
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(armor), Is.EqualTo(7f),
                "WithVariable must not overwrite an existing base value.");
        }

        [Test]
        public void AggregateSubscription_FiresOnRebuild_WithFreshRecord()
        {
            var (store, _, _, id) = CreateEntity();
            var captured = new List<float>();
            store.Subscribe<StateEntity<EntityDefinition>>(id, (_, e, _) => captured.Add(e.GetVariable<float>(hp)));

            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(2f), ModifierId.New()));

            Assert.That(captured.Count, Is.GreaterThanOrEqualTo(2), "Subscription should fire at least once per Execute that mutates the source slice.");
            Assert.That(captured[captured.Count - 1], Is.EqualTo(17f), "Final callback should observe the fully-rebuilt aggregate (10 + 5 + 2).");
        }

        [Test]
        public void Snapshot_DoesNotIncludeAggregateState()
        {
            var (store, _, _, id) = CreateEntity();
            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));

            var snapshot = store.SaveSnapshot();

            Assert.That(snapshot.Contains(id, typeof(EntityVariableState)), Is.True, "Authored canonical state must be in the snapshot.");
            Assert.That(snapshot.Contains(id, typeof(StateEntity<EntityDefinition>)), Is.False, "Aggregate state must not be in the snapshot — it is rebuilt on load.");
        }

        [Test]
        public void TwoEntities_SnapshotRoundTrip_RebuildsBothAggregates()
        {
            var heroDef = new EntityDefinition();
            heroDef.AddVariable(hp, new FloatVariableValue(10f));
            var goblinDef = new EntityDefinition();
            goblinDef.AddVariable(hp, new FloatVariableValue(30f));

            var store = new StoreBuilder().Build();
            EntityBridgeContext.RegisterMutators(store);

            var heroId = new InstanceId(1);
            var goblinId = new InstanceId(2);
            EntityStateFactory.Create(heroDef, store, heroId);
            EntityStateFactory.Create(goblinDef, store, goblinId);

            var snapshot = store.SaveSnapshot();
            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New()));
            store.Execute(goblinId, new AddModifierPayload(goblinId, hp, new FloatAddModifier(3f), ModifierId.New()));
            Assert.That(store.Get<StateEntity<EntityDefinition>>(heroId).GetVariable<float>(hp), Is.EqualTo(15f));
            Assert.That(store.Get<StateEntity<EntityDefinition>>(goblinId).GetVariable<float>(hp), Is.EqualTo(33f));

            store.LoadSnapshot(snapshot);

            Assert.That(store.Get<StateEntity<EntityDefinition>>(heroId).GetVariable<float>(hp), Is.EqualTo(10f));
            Assert.That(store.Get<StateEntity<EntityDefinition>>(goblinId).GetVariable<float>(hp), Is.EqualTo(30f));
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

            EntityStateFactory.Create(heroDef, store, heroId);
            EntityStateFactory.Create(goblinDef, store, goblinId);

            store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New()));

            Assert.That(store.Get<StateEntity<EntityDefinition>>(heroId).GetVariable<float>(hp), Is.EqualTo(15f),
                "Hero should be base 10 + modifier 5 applied exactly once.");
            Assert.That(store.Get<StateEntity<EntityDefinition>>(goblinId).GetVariable<float>(hp), Is.EqualTo(30f),
                "Goblin should be untouched by a modifier targeted at hero.");
        }

        [Test]
        public void Aggregate_RebuildsAfterLoadSnapshot()
        {
            var (store, _, _, id) = CreateEntity();
            var snapshot = store.SaveSnapshot();

            store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(15f));

            store.LoadSnapshot(snapshot);
            Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(10f),
                "After LoadSnapshot the source slice is restored and the aggregate must rebuild to the original effective value.");
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

            var hero = EntityStateFactory.Create(heroDef, store, new InstanceId(1));
            var goblin = EntityStateFactory.Create(goblinDef, store, new InstanceId(2));

            Assert.That(hero.GetVariable<float>(hp), Is.EqualTo(10f));
            Assert.That(goblin.GetVariable<float>(hp), Is.EqualTo(30f));
        }

        private static (Store store, EntityDefinition def, StateEntity<EntityDefinition> entity, InstanceId id) CreateEntity()
        {
            var def = new EntityDefinition();
            def.AddVariable(hp, new FloatVariableValue(10f));

            var builder = new StoreBuilder();
            var store = builder.Build();
            EntityBridgeContext.RegisterMutators(store);

            var id = new InstanceId(1);
            var entity = EntityStateFactory.Create(def, store, id);
            return (store, def, entity, id);
        }
    }
}

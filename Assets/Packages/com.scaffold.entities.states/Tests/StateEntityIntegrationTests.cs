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
        public void StateEntity_DoesNotImplement_IMutableEntity()
        {
            var (_, _, entity, _) = CreateEntity();
            var entityType = entity.GetType();
            Assert.That(typeof(IMutableEntity<EntityDefinition>).IsAssignableFrom(entityType), Is.False);
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

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
            var payload = new AddModifierPayload(id, hp, addMod, modId);
            store.Execute(id, payload);
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
            var add = new FloatAddModifier(3f);
            var mul = new FloatMultiplyModifier(4f);
            var idA = ModifierId.New();
            var idB = ModifierId.New();
            var addPayload = new AddModifierPayload(id, hp, add, idA);
            var mulPayload = new AddModifierPayload(id, hp, mul, idB);
            store.Execute(id, addPayload);
            store.Execute(id, mulPayload);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(52f));
        }

        [Test]
        public void RemoveModifier_ByModifierId_RestoresPriorValue()
        {
            var (store, _, entity, id) = CreateEntity();
            var addMod = new FloatAddModifier(5f);
            var modId = ModifierId.New();
            var addPayload = new AddModifierPayload(id, hp, addMod, modId);
            store.Execute(id, addPayload);
            var removePayload = new RemoveModifierPayload(id, hp, modId);
            store.Execute(id, removePayload);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void Snapshot_RoundTripsModifierStack()
        {
            var (store, _, entity, id) = CreateEntity();
            var snapshot = store.SaveSnapshot();

            var addMod = new FloatAddModifier(5f);
            var modId = ModifierId.New();
            var payload = new AddModifierPayload(id, hp, addMod, modId);
            store.Execute(id, payload);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));

            store.LoadSnapshot(snapshot);
            Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
        }

        [Test]
        public void StateEntity_DoesNotImplement_IMutableEntity()
        {
            var (_, _, entity, _) = CreateEntity();
            var entityType = entity.GetType();
            Assert.That(typeof(IMutableEntity<EntityDefinition>).IsAssignableFrom(entityType), Is.False);
        }

        private static (Store store, EntityDefinition def, StateEntity<EntityDefinition> entity, InstanceId id) CreateEntity()
        {
            var def = new EntityDefinition();
            def.AddVariable(hp, new FloatVariableValue(10f));

            var builder = new StoreBuilder();
            var store = builder.Build();

            var id = new InstanceId(1);
            var entity = EntityStateFactory.Create(def, store, id);
            return (store, def, entity, id);
        }
    }
}

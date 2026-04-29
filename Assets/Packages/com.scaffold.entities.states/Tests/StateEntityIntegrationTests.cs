using NUnit.Framework;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States.Tests
{
    public class StateEntityIntegrationTests
    {
        private static readonly Variable Hp = new("hp", "float");

        private static (Store store, EntityDefinition def, StateEntity<EntityDefinition> entity, InstanceId id)
            MakeEntity()
        {
            var def = new EntityDefinition();
            def.AddVariable(Hp, new FloatVariableValue(10f));

            var builder = new StoreBuilder();
            var store = builder.Build();

            var id = new InstanceId(1);
            var entity = EntityStateFactory.Create(def, store, id);
            return (store, def, entity, id);
        }

        [Test]
        public void GetVariable_ReturnsDefinitionDefault_WhenNoOverridesOrModifiers()
        {
            var (_, _, entity, _) = MakeEntity();
            Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(10f));
        }

        [Test]
        public void AddModifier_ChangesEffectiveValue()
        {
            var (store, _, entity, id) = MakeEntity();
            store.Execute(id, new AddModifierPayload(id, Hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(15f));
        }

        [Test]
        public void RemoveModifier_ByModifierId_RestoresPriorValue()
        {
            var (store, _, entity, id) = MakeEntity();
            var modId = ModifierId.New();
            store.Execute(id, new AddModifierPayload(id, Hp, new FloatAddModifier(5f), modId));
            store.Execute(id, new RemoveModifierPayload(id, Hp, modId));
            Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(10f));
        }

        [Test]
        public void Modifier_OrderIsRespected_AddBeforeMultiply()
        {
            // Default Order is 0 for both modifier types; tie-break is insertion order.
            // Adding add then multiply yields fold (10 + 3) * 4 = 52. Reversing order would yield 43.
            var (store, _, entity, id) = MakeEntity();
            var add = new FloatAddModifier(3f);
            var mul = new FloatMultiplyModifier(4f);
            store.Execute(id, new AddModifierPayload(id, Hp, add, ModifierId.New()));
            store.Execute(id, new AddModifierPayload(id, Hp, mul, ModifierId.New()));
            Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(52f));
        }

        [Test]
        public void Snapshot_RoundTripsModifierStack()
        {
            var (store, _, entity, id) = MakeEntity();
            var snapshot = store.SaveSnapshot();

            store.Execute(id, new AddModifierPayload(id, Hp, new FloatAddModifier(5f), ModifierId.New()));
            Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(15f));

            store.LoadSnapshot(snapshot);
            Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(10f));
        }

        [Test]
        public void StateEntity_DoesNotImplement_IMutableEntity()
        {
            var (_, _, entity, _) = MakeEntity();
            Assert.IsFalse(
                entity is IMutableEntity<EntityDefinition>,
                "StateEntity must not be assignable to IMutableEntity — writes go through the store.");
        }
    }
}

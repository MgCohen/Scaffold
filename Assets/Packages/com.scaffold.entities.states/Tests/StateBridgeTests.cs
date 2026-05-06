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

        [Test]
        public void Spawn_DefaultRead_ReturnsDefinitionDefault()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var def = Def(hp, 10);
            var entityRef = store.Catalog.AllocateRef<EntityInstance<EntityDefinition>>();
            var entity = EntityStateFactory.Create(def, store, entityRef);

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(10));
        }

        [Test]
        public void SetBaseValue_ThenRead_ReturnsBase()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var def = Def(hp, 10);
            var entityRef = store.Catalog.AllocateRef<EntityInstance<EntityDefinition>>();
            var entity = EntityStateFactory.Create(def, store, entityRef);

            entity.SetBaseValue(hp, Int(7));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(7));
        }

        [Test]
        public void AddModifier_AppliesToBase()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var def = Def(hp, 10);
            var entityRef = store.Catalog.AllocateRef<EntityInstance<EntityDefinition>>();
            var entity = EntityStateFactory.Create(def, store, entityRef);

            entity.SetBaseValue(hp, Int(5));
            entity.AddModifier(hp, new IntAddModifier(3));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(8));
        }

        [Test]
        public void RemoveModifier_RestoresBase()
        {
            var store = NewStoreWithBridge();
            var hp = Var("hp");
            var def = Def(hp, 10);
            var entityRef = store.Catalog.AllocateRef<EntityInstance<EntityDefinition>>();
            var entity = EntityStateFactory.Create(def, store, entityRef);
            ModifierId id = entity.AddModifier(hp, new IntAddModifier(5));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(15));
            entity.RemoveModifier(hp, id);
            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(10));
        }
    }
}

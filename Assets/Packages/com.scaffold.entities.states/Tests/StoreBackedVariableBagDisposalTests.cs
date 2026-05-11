using System;
using NUnit.Framework;
using Scaffold.Entities;
using Scaffold.Entities.States;
using Scaffold.States;
using Scaffold.Variables;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States.Tests
{
    public sealed class StoreBackedVariableBagDisposalTests
    {
        private static Variable Hp() => new Variable("hp", "int");

        private static (Store store, Ref<EntityState> entityRef) NewStore(int initialHp = 10)
        {
            var builder = new StoreBuilder();
            EntityBridgeContext.RegisterMutators(builder);
            var store = builder.Build();
            var entityRef = new Ref<EntityState>(Guid.NewGuid());
            store.RegisterSlice(entityRef,
                EntityState.Empty.WithBaseValue(Hp(), new IntVariableValue(initialHp)));
            return (store, entityRef);
        }

        [Test]
        public void Dispose_UnsubscribesAllStoreSubscriptions()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            bag.TryGet<int>("hp", out var handle);
            int fires = 0;
            handle.Subscribe(_ => fires++);

            store.Execute(new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(50)));
            Assert.That(fires, Is.EqualTo(1));

            bag.Dispose();

            store.Execute(new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(99)));
            Assert.That(fires, Is.EqualTo(1), "After Dispose the bag must not receive further store events.");
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var (store, entityRef) = NewStore();
            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            bag.Dispose();
            Assert.DoesNotThrow(() => bag.Dispose());
        }

        [Test]
        public void Dispose_ClearsLocalHandles()
        {
            var (store, entityRef) = NewStore();
            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            Assert.That(System.Linq.Enumerable.Count(bag.LocalHandles), Is.EqualTo(1));
            bag.Dispose();
            Assert.That(System.Linq.Enumerable.Count(bag.LocalHandles), Is.EqualTo(0));
        }
    }
}

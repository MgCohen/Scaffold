using NUnit.Framework;
using Scaffold.Entities;
using Scaffold.Entities.States;
using Scaffold.States;
using Scaffold.Variables;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States.Tests
{
    public sealed class StoreBackedHandleDeferralTests
    {
        private static Variable Hp() => new Variable("hp", "int");

        private static (Store store, Ref<EntityState> entityRef) NewStoreWithDeferredEvents(int initialHp)
        {
            var inner = StateEventHandlerFactory.CreateDefault();
            var deferred = new DeferredStateEventHandler(inner, StateEventMergeMode.LatestPerKey);

            var builder = new StoreBuilder();
            builder.AddEventHandler(deferred);
            EntityBridgeContext.RegisterMutators(builder);
            var store = builder.Build();

            var entityRef = new Ref<EntityState>(System.Guid.NewGuid());
            var initial = EntityState.Empty.WithBaseValue(Hp(), new IntVariableValue(initialHp));
            store.RegisterSlice(entityRef, initial);

            return (store, entityRef);
        }

        // Test 1: ExecuteBatch coalesces multiple writes against the same slice into a
        // single subscriber fire carrying the post-merge value. The Store's overlay
        // commits one Notify per (ref, type) and the LatestPerKey handler delivers it
        // once.
        [Test]
        public void ExecuteBatch_TwoSetsOnSameSlice_HandleFiresOnceWithFinalValue()
        {
            var (store, entityRef) = NewStoreWithDeferredEvents(initialHp: 10);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            Assert.That(bag.TryGet<int>("hp", out var handle), Is.True);

            int fireCount = 0;
            int lastValue = 0;
            handle.Subscribe(v => { fireCount++; lastValue = v; });

            store.ExecuteBatch(new object[]
            {
                new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(50)),
                new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(75)),
            });

            Assert.That(fireCount, Is.EqualTo(1), "Subscriber should fire exactly once per ExecuteBatch (LatestPerKey coalesce).");
            Assert.That(lastValue, Is.EqualTo(75));
            Assert.That(handle.Value, Is.EqualTo(75));

            bag.Dispose();
        }

        // Test 2: User-controlled defer scope coalesces an Execute + LoadSnapshot pair
        // whose net effect is identity. Subscriber must not fire.
        [Test]
        public void DeferScope_ExecuteThenSnapshotRevertToInitial_HandleDoesNotFire()
        {
            var (store, entityRef) = NewStoreWithDeferredEvents(initialHp: 10);
            var snapshot = store.SaveSnapshot();

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            Assert.That(bag.TryGet<int>("hp", out var handle), Is.True);

            int fireCount = 0;
            handle.Subscribe(_ => fireCount++);

            var deferral = (IStateEventDeferralController)store.Events;
            using (deferral.BeginDeferScope())
            {
                store.Execute(new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(50)));
                store.LoadSnapshot(snapshot);
            }
            deferral.Flush();

            Assert.That(fireCount, Is.EqualTo(0), "Net-zero state change in a defer scope must not fire subscribers.");
            Assert.That(handle.Value, Is.EqualTo(10));

            bag.Dispose();
        }

        // Test 3: User-controlled defer scope coalesces multiple sets into a single
        // fire that carries the final post-merge value. Intermediate mutations are
        // not observable.
        [Test]
        public void DeferScope_MultipleSets_HandleFiresOnceWithFinalValue()
        {
            var (store, entityRef) = NewStoreWithDeferredEvents(initialHp: 10);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            Assert.That(bag.TryGet<int>("hp", out var handle), Is.True);

            int fireCount = 0;
            int lastValue = 0;
            handle.Subscribe(v => { fireCount++; lastValue = v; });

            var deferral = (IStateEventDeferralController)store.Events;
            using (deferral.BeginDeferScope())
            {
                store.Execute(new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(50)));
                store.Execute(new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(75)));
            }
            deferral.Flush();

            Assert.That(fireCount, Is.EqualTo(1), "Multiple sets in one defer scope must collapse to a single fire.");
            Assert.That(lastValue, Is.EqualTo(75));
            Assert.That(handle.Value, Is.EqualTo(75));

            bag.Dispose();
        }
    }
}

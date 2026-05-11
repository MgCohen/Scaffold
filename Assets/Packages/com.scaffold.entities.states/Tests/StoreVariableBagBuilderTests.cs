using System;
using NUnit.Framework;
using Scaffold.Entities;
using Scaffold.Entities.States;
using Scaffold.States;
using Scaffold.Variables;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States.Tests
{
    public sealed class StoreVariableBagBuilderTests
    {
        private static Variable Hp() => new Variable("hp", "int");
        private static Variable Mana() => new Variable("mana", "int");

        private static (Store store, Ref<EntityState> entityRef) NewStore(int initialHp = 10, int? initialMana = null)
        {
            var builder = new StoreBuilder();
            EntityBridgeContext.RegisterMutators(builder);
            var store = builder.Build();
            var entityRef = new Ref<EntityState>(Guid.NewGuid());

            var initial = EntityState.Empty.WithBaseValue(Hp(), new IntVariableValue(initialHp));
            if (initialMana.HasValue)
                initial = initial.WithBaseValue(Mana(), new IntVariableValue(initialMana.Value));
            store.RegisterSlice(entityRef, initial);
            return (store, entityRef);
        }

        [Test]
        public void BindBase_ReadsEntityStateBaseValue()
        {
            var (store, entityRef) = NewStore(initialHp: 42);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            Assert.That(bag.TryGet<int>("hp", out var handle), Is.True);
            Assert.That(handle.Value, Is.EqualTo(42));

            bag.Dispose();
        }

        [Test]
        public void BindBase_Set_WritesThroughStore()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            Assert.That(bag.TryGet<int>("hp", out var handle), Is.True);
            handle.Set(33);

            Assert.That(handle.Value, Is.EqualTo(33));
            Assert.That(store.Get<EntityState>(entityRef).TryGetBase(Hp(), out var bv), Is.True);
            Assert.That(((IntVariableValue)bv).Value, Is.EqualTo(33));

            bag.Dispose();
        }

        [Test]
        public void BindBase_SubscribeFiresOnExternalStoreExecute()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            bag.TryGet<int>("hp", out var handle);
            int observed = 0;
            handle.Subscribe(v => observed = v);

            store.Execute(new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(99)));

            Assert.That(observed, Is.EqualTo(99));
            bag.Dispose();
        }

        [Test]
        public void BindBase_SubscribeDoesNotRefireOnIdenticalSet()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .Build();

            bag.TryGet<int>("hp", out var handle);
            int fireCount = 0;
            handle.Subscribe(_ => fireCount++);

            handle.Set(10); // same as initial
            Assert.That(fireCount, Is.EqualTo(0));

            handle.Set(20);
            Assert.That(fireCount, Is.EqualTo(1));

            handle.Set(20); // same as last fired
            Assert.That(fireCount, Is.EqualTo(1));

            bag.Dispose();
        }

        [Test]
        public void BindComputed_ReadIsBaseWhenNoModifiers()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindComputed<int>("hp", Hp())
                .Build();

            Assert.That(bag.TryGetReadOnly<int>("hp", out var handle), Is.True);
            Assert.That(handle.Value, Is.EqualTo(10));

            bag.Dispose();
        }

        [Test]
        public void BindComputed_ReadIsBasePlusModifiers()
        {
            var (store, entityRef) = NewStore(initialHp: 10);
            store.Execute(new AddModifierPayload(entityRef, Hp(), new IntAddModifier(5), ModifierId.New(), null));

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindComputed<int>("hp", Hp())
                .Build();

            Assert.That(bag.TryGetReadOnly<int>("hp", out var handle), Is.True);
            Assert.That(handle.Value, Is.EqualTo(15));

            bag.Dispose();
        }

        [Test]
        public void BindComputed_WritableTryGetReturnsFalse()
        {
            // Read-only bindings are not discoverable via the writable TryGet<T>.
            var (store, entityRef) = NewStore(initialHp: 10);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindComputed<int>("hp", Hp())
                .Build();

            Assert.That(bag.TryGet<int>("hp", out _), Is.False);
            bag.Dispose();
        }

        [Test]
        public void Bind_GenericSlice_ReadsAndWrites()
        {
            var (store, entityRef) = NewStore(initialHp: 7);

            var bag = new StoreVariableBagBuilder(store)
                .ForSlice<EntityState>(entityRef)
                .Bind<int, SetBaseValuePayload>(
                    "hp",
                    s => s.TryGetBase(Hp(), out var v) && v is IVariableValue<int> tv ? tv.Get() : 0,
                    v => new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(v)))
                .Build();

            bag.TryGet<int>("hp", out var handle);
            Assert.That(handle.Value, Is.EqualTo(7));
            handle.Set(123);
            Assert.That(handle.Value, Is.EqualTo(123));

            bag.Dispose();
        }

        [Test]
        public void Build_ThrowsOnUnregisteredPayload()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var builder = new StoreVariableBagBuilder(store)
                .ForSlice<EntityState>(entityRef)
                .Bind<int, UnknownPayload>(
                    "hp",
                    s => 0,
                    v => new UnknownPayload(v));

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void Build_ThrowsOnDuplicateVariableId()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var scope = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef);
            scope.BindBase<int>("hp", Hp());

            Assert.Throws<InvalidOperationException>(() =>
                new StoreVariableBagBuilder(store)
                    .ForEntity(entityRef)
                    .BindBase<int>("hp", Hp())
                    .ForEntity(entityRef)
                    .BindBase<int>("hp", Hp())
                    .Build());
        }

        [Test]
        public void WithFallback_InMemoryDefault_MaterializesUnboundIds()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var seed = new (string id, VariableDefault? @default)[]
            {
                ("hp", new IntDefaultForTests { value = 0 }),
                ("score", new IntDefaultForTests { value = 1000 }),
            };

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .WithFallback(seed, FallbackMode.InMemoryDefault)
                .Build();

            Assert.That(bag.TryGet<int>("score", out var fallback), Is.True);
            Assert.That(fallback.Value, Is.EqualTo(1000));
            // Bound entry still wins (no double-add).
            Assert.That(bag.TryGet<int>("hp", out var bound), Is.True);
            Assert.That(bound.Value, Is.EqualTo(10));

            bag.Dispose();
        }

        [Test]
        public void WithFallback_Throw_ThrowsOnUnboundSeed()
        {
            var (store, entityRef) = NewStore(initialHp: 10);

            var seed = new (string id, VariableDefault? @default)[]
            {
                ("hp", new IntDefaultForTests { value = 0 }),
                ("score", new IntDefaultForTests { value = 0 }),
            };

            var builder = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .WithFallback(seed, FallbackMode.Throw);

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void Build_GroupsBindingsBySlice_OneSubscriptionPerGroup()
        {
            // Two bindings against the same (ref, EntityState) slice should fire
            // from a single store-side subscription. Trigger a single Execute
            // and confirm both handles' subscribers fire exactly once.
            var (store, entityRef) = NewStore(initialHp: 10, initialMana: 5);

            var bag = new StoreVariableBagBuilder(store)
                .ForEntity(entityRef)
                .BindBase<int>("hp", Hp())
                .ForEntity(entityRef)
                .BindBase<int>("mana", Mana())
                .Build();

            bag.TryGet<int>("hp", out var hpHandle);
            bag.TryGet<int>("mana", out var manaHandle);

            int hpFires = 0, manaFires = 0;
            hpHandle.Subscribe(_ => hpFires++);
            manaHandle.Subscribe(_ => manaFires++);

            store.Execute(new SetBaseValuePayload(entityRef, Hp(), new IntVariableValue(99)));

            Assert.That(hpFires, Is.EqualTo(1));
            // mana was unchanged so its handle should not fire.
            Assert.That(manaFires, Is.EqualTo(0));

            store.Execute(new SetBaseValuePayload(entityRef, Mana(), new IntVariableValue(77)));
            Assert.That(manaFires, Is.EqualTo(1));
            // hp still equal to its last value -> no extra fire
            Assert.That(hpFires, Is.EqualTo(1));

            bag.Dispose();
        }

        [Test]
        public void NullStore_ConstructorThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new StoreVariableBagBuilder(null!));
        }

        private sealed class UnknownPayload
        {
            public int Value;
            public UnknownPayload(int value) { Value = value; }
        }

        // Lightweight VariableDefault<int> for fallback tests. The shared
        // package only ships abstract VariableDefault<T>; tests provide their
        // own concrete subclass without depending on graphflow.
        private sealed class IntDefaultForTests : VariableDefault<int> { }
    }
}

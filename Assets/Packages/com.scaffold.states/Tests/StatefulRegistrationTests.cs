#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;

namespace Scaffold.States.Tests
{
    public class StatefulRegistrationTests
    {
        private sealed record HealthState(int Value) : State;

        private sealed record OwnerState(string OwnerName) : State;

        private sealed class Card : ISliceProvider
        {
            public string Name { get; }
            public int InitialHealth { get; }
            public string Owner { get; }

            public Card(string name, int health, string owner)
            {
                Name = name;
                InitialHealth = health;
                Owner = owner;
            }

            public IEnumerable<State> ProvideInitialSlices()
            {
                yield return new HealthState(InitialHealth);
                yield return new OwnerState(Owner);
            }
        }

        private sealed class CataloggedCard : ICatalogged, ISliceProvider
        {
            public Guid Key { get; }
            public int InitialHealth { get; }

            public CataloggedCard(Guid key, int health)
            {
                Key = key;
                InitialHealth = health;
            }

            public IEnumerable<State> ProvideInitialSlices()
            {
                yield return new HealthState(InitialHealth);
            }
        }

        private sealed class EmptySlicesCard : ISliceProvider
        {
            public IEnumerable<State> ProvideInitialSlices() => Array.Empty<State>();
        }

        // ---- RegisterEntity / UnregisterEntity --------------------------------

        [Test]
        public void RegisterEntity_BindsRefAndRegistersSlices()
        {
            var store = new StoreBuilder().Build();
            var card = new Card("ace", 10, "Alice");

            Ref<Card> @ref = store.RegisterEntity(card);

            Assert.AreSame(card, store.Catalog.Resolve(@ref));
            Assert.AreEqual(10, store.Get<HealthState>(@ref).Value);
            Assert.AreEqual("Alice", store.Get<OwnerState>(@ref).OwnerName);
        }

        [Test]
        public void RegisterEntity_ICatalogged_UsesDeclaredKey()
        {
            var store = new StoreBuilder().Build();
            var key = Guid.NewGuid();
            var card = new CataloggedCard(key, 7);

            Ref<CataloggedCard> @ref = store.RegisterEntity(card);

            Assert.AreEqual(key, @ref.Id);
            Assert.AreEqual(7, store.Get<HealthState>(@ref).Value);
        }

        [Test]
        public void RegisterEntity_EmptySliceList_RegistersCatalogOnly()
        {
            var store = new StoreBuilder().Build();
            var entity = new EmptySlicesCard();

            Ref<EmptySlicesCard> @ref = store.RegisterEntity(entity);

            Assert.AreSame(entity, store.Catalog.Resolve(@ref));
            Assert.IsFalse(store.TryGet<HealthState>(@ref, out _));
        }

        [Test]
        public void UnregisterEntity_RemovesCatalogAndSlices()
        {
            var store = new StoreBuilder().Build();
            var card = new Card("ace", 10, "Alice");
            Ref<Card> @ref = store.RegisterEntity(card);

            bool removed = store.UnregisterEntity(@ref);

            Assert.IsTrue(removed);
            Assert.IsFalse(store.Catalog.TryResolve(@ref, out _));
            Assert.IsFalse(store.TryGet<HealthState>(@ref, out _));
            Assert.IsFalse(store.TryGet<OwnerState>(@ref, out _));
        }

        [Test]
        public void UnregisterEntity_NotRegistered_ReturnsFalse()
        {
            var store = new StoreBuilder().Build();
            var ghost = new Ref<Card>(Guid.NewGuid());

            bool removed = store.UnregisterEntity(ghost);

            Assert.IsFalse(removed);
        }

        [Test]
        public void RegisterEntity_NullEntity_Throws()
        {
            var store = new StoreBuilder().Build();
            Assert.Throws<ArgumentNullException>(() => store.RegisterEntity<Card>(null!));
        }

        [Test]
        public void UnregisterEntity_NullRef_Throws()
        {
            var store = new StoreBuilder().Build();
            Assert.Throws<ArgumentNullException>(() => store.UnregisterEntity<Card>(null!));
        }

        // ---- Ref<T> sugar ------------------------------------------------------

        [Test]
        public void Ref_Resolve_InfersTypeFromReceiver()
        {
            var store = new StoreBuilder().Build();
            var card = new Card("ace", 10, "Alice");
            Ref<Card> @ref = store.RegisterEntity(card);

            Card resolved = @ref.Resolve(store);

            Assert.AreSame(card, resolved);
        }

        [Test]
        public void Ref_TryResolve_InfersTypeAndReturnsTrue()
        {
            var store = new StoreBuilder().Build();
            var card = new Card("ace", 10, "Alice");
            Ref<Card> @ref = store.RegisterEntity(card);

            bool ok = @ref.TryResolve(store, out Card? resolved);

            Assert.IsTrue(ok);
            Assert.AreSame(card, resolved);
        }

        [Test]
        public void Ref_TryResolve_AfterUnregister_ReturnsFalse()
        {
            var store = new StoreBuilder().Build();
            Ref<Card> @ref = store.RegisterEntity(new Card("ace", 10, "Alice"));
            store.UnregisterEntity(@ref);

            bool ok = @ref.TryResolve(store, out Card? resolved);

            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
        }

        // ---- Reference sugar (slice access) -----------------------------------

        [Test]
        public void Reference_GetSlice_ResolvesThroughRefT()
        {
            var store = new StoreBuilder().Build();
            Ref<Card> @ref = store.RegisterEntity(new Card("ace", 10, "Alice"));

            HealthState health = @ref.GetSlice<HealthState>(store);

            Assert.AreEqual(10, health.Value);
        }

        [Test]
        public void Reference_GetSlice_WorksOnNullReference()
        {
            var builder = new StoreBuilder();
            builder.AddState(new HealthState(42));
            var store = builder.Build();

            HealthState health = Reference.Null.GetSlice<HealthState>(store);

            Assert.AreEqual(42, health.Value);
        }

        [Test]
        public void Reference_TryGetSlice_ReturnsTrueWhenPresent()
        {
            var store = new StoreBuilder().Build();
            Ref<Card> @ref = store.RegisterEntity(new Card("ace", 10, "Alice"));

            bool ok = @ref.TryGetSlice(store, out HealthState health);

            Assert.IsTrue(ok);
            Assert.AreEqual(10, health.Value);
        }

        [Test]
        public void Reference_TryGetSlice_ReturnsFalseAfterUnregister()
        {
            var store = new StoreBuilder().Build();
            Ref<Card> @ref = store.RegisterEntity(new Card("ace", 10, "Alice"));
            store.UnregisterEntity(@ref);

            bool ok = @ref.TryGetSlice(store, out HealthState _);

            Assert.IsFalse(ok);
        }

        // ---- Stable-enumeration contract --------------------------------------

        [Test]
        public void RegisterEntity_ReCallsProvideInitialSlicesOnUnregister()
        {
            // The unregister flow re-calls ProvideInitialSlices() to collect
            // slice types. This test pins that the same enumeration is acceptable
            // input to UnregisterSlice — i.e., types that registered fine also
            // unregister fine.
            var store = new StoreBuilder().Build();
            var card = new Card("ace", 10, "Alice");
            Ref<Card> @ref = store.RegisterEntity(card);

            Assert.IsTrue(store.UnregisterEntity(@ref));
            Assert.IsFalse(store.UnregisterEntity(@ref));
        }

        // ---- Integration: snapshot round-trip on entity-with-cross-ref --------

        private sealed record OwnedByState(Ref<Card> Owner) : State;

        private sealed class Token : ISliceProvider
        {
            private readonly Ref<Card> owner;
            public Token(Ref<Card> owner) { this.owner = owner; }
            public IEnumerable<State> ProvideInitialSlices()
            {
                yield return new OwnedByState(owner);
            }
        }

        [Test]
        public void SnapshotRoundTrip_TokenWithRefToCard_ResolvesAfterLoad()
        {
            var store = new StoreBuilder().Build();
            var card = new Card("ace", 10, "Alice");
            Ref<Card> cardRef = store.RegisterEntity(card);

            Ref<Token> tokenRef = store.RegisterEntity(new Token(cardRef));
            Snapshot snap = store.SaveSnapshot();

            store.UnregisterSlice<OwnedByState>(tokenRef);
            store.LoadSnapshot(snap);

            OwnedByState restored = tokenRef.GetSlice<OwnedByState>(store);
            Assert.AreEqual(cardRef, restored.Owner);
            Assert.AreSame(card, restored.Owner.Resolve(store));
        }
    }
}

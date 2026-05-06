#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public class CatalogTests
    {
        private sealed class Card
        {
            public string Name { get; }
            public Card(string name) { Name = name; }
        }

        private sealed class Zone
        {
            public string Name { get; }
            public Zone(string name) { Name = name; }
        }

        private sealed class CataloggedCard : ICatalogged
        {
            public Guid Key { get; }
            public string Name { get; }
            public CataloggedCard(Guid key, string name) { Key = key; Name = name; }
        }

        private static ICatalog NewCatalog() => new Catalog();

        // ---- Register / auto-id ----------------------------------------------------

        [Test]
        public void Register_NonICatalogged_AssignsAutoId()
        {
            var catalog = NewCatalog();
            var card = new Card("ace");
            var @ref = catalog.Register(card);

            Assert.AreNotEqual(Guid.Empty, @ref.Id);
            Assert.AreSame(card, catalog.Resolve(@ref));
        }

        [Test]
        public void Register_ICatalogged_UsesKey()
        {
            var catalog = NewCatalog();
            var key = Guid.NewGuid();
            var card = new CataloggedCard(key, "ace");

            var @ref = catalog.Register(card);

            Assert.AreEqual(key, @ref.Id);
        }

        [Test]
        public void Register_TwoEquivalentICatalogged_ReturnsSameRef()
        {
            var catalog = NewCatalog();
            var key = Guid.NewGuid();
            var card = new CataloggedCard(key, "ace");

            var first = catalog.Register(card);
            var second = catalog.Register(card);

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.Id, second.Id);
        }

        [Test]
        public void Register_TwoNonICatalogged_ReturnsDifferentRefs()
        {
            var catalog = NewCatalog();
            var first = catalog.Register(new Card("a"));
            var second = catalog.Register(new Card("b"));

            Assert.AreNotEqual(first.Id, second.Id);
        }

        [Test]
        public void Register_ICatalogged_DifferentObjectSameKey_Throws()
        {
            var catalog = NewCatalog();
            var key = Guid.NewGuid();
            catalog.Register(new CataloggedCard(key, "first"));

            Assert.Throws<InvalidOperationException>(
                () => catalog.Register(new CataloggedCard(key, "second")));
        }

        // ---- Two-step Allocate / RegisterAt ---------------------------------------

        [Test]
        public void AllocateThenRegisterAt_ResolvesSuccessfully()
        {
            var catalog = NewCatalog();
            var @ref = catalog.AllocateRef<Card>();
            var card = new Card("ace");

            catalog.RegisterAt(@ref, card);

            Assert.AreSame(card, catalog.Resolve(@ref));
        }

        [Test]
        public void RegisterAt_BeforeAllocate_Throws()
        {
            var catalog = NewCatalog();
            var fake = new Ref<Card>(Guid.NewGuid());

            Assert.Throws<InvalidOperationException>(
                () => catalog.RegisterAt(fake, new Card("ace")));
        }

        [Test]
        public void RegisterAt_DifferentObjectAtBoundRef_Throws()
        {
            var catalog = NewCatalog();
            var @ref = catalog.AllocateRef<Card>();
            catalog.RegisterAt(@ref, new Card("first"));

            Assert.Throws<InvalidOperationException>(
                () => catalog.RegisterAt(@ref, new Card("second")));
        }

        [Test]
        public void RegisterAt_SameObjectAtBoundRef_Idempotent()
        {
            var catalog = NewCatalog();
            var @ref = catalog.AllocateRef<Card>();
            var card = new Card("ace");

            catalog.RegisterAt(@ref, card);
            Assert.DoesNotThrow(() => catalog.RegisterAt(@ref, card));
            Assert.AreSame(card, catalog.Resolve(@ref));
        }

        // ---- Resolve / TryResolve / Unregister ------------------------------------

        [Test]
        public void Resolve_UnregisteredRef_Throws()
        {
            var catalog = NewCatalog();
            var @ref = catalog.Register(new Card("ace"));
            catalog.Unregister(@ref);

            Assert.Throws<KeyNotFoundException>(() => catalog.Resolve(@ref));
        }

        [Test]
        public void TryResolve_UnregisteredRef_ReturnsFalse()
        {
            var catalog = NewCatalog();
            var fake = new Ref<Card>(Guid.NewGuid());

            Assert.IsFalse(catalog.TryResolve(fake, out var obj));
            Assert.IsNull(obj);
        }

        [Test]
        public void Resolve_RegisteredUnderDifferentT_ReturnsFalse()
        {
            var catalog = NewCatalog();
            var card = new Card("ace");
            var cardRef = catalog.Register(card);

            // Construct a Ref<Zone> with the same Guid — the catalog secondary key
            // (typeof(T)) discriminates, so the lookup misses.
            var zoneRef = new Ref<Zone>(cardRef.Id);

            Assert.IsFalse(catalog.TryResolve(zoneRef, out _));
        }

        [Test]
        public void RegisterAt_AllocatedUnderDifferentT_Throws()
        {
            var catalog = NewCatalog();
            var allocated = catalog.AllocateRef<Card>();
            var zoneRef = new Ref<Zone>(allocated.Id);

            Assert.Throws<InvalidOperationException>(
                () => catalog.RegisterAt(zoneRef, new Zone("z")));
        }

        // ---- Ref equality / hash ---------------------------------------------------

        [Test]
        public void RefEquality_SameGuidSameT_AreEqual()
        {
            var g = Guid.NewGuid();
            var a = new Ref<Card>(g);
            var b = new Ref<Card>(g);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
        }

        [Test]
        public void RefEquality_SameGuidDifferentT_NotEqual()
        {
            var g = Guid.NewGuid();
            Reference a = new Ref<Card>(g);
            Reference b = new Ref<Zone>(g);

            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void RefHash_SameGuidSameT_SameHash()
        {
            var g = Guid.NewGuid();
            var a = new Ref<Card>(g);
            var b = new Ref<Card>(g);

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        // ---- Slice integration -----------------------------------------------------

        [Test]
        public void RefAsReference_FlowsIntoSliceAPIs()
        {
            var builder = new StoreBuilder();
            var cardRef = new Ref<Card>(Guid.NewGuid());
            builder.AddState(cardRef, new CounterState(7));
            var store = builder.Build();

            var state = store.Get<CounterState>(cardRef);

            Assert.AreEqual(7, state.Value);
        }

        // ---- Snapshot contract -----------------------------------------------------

        [Test]
        public void Store_Catalog_PropertyAlwaysSet()
        {
            var simple = new Store(StateEventHandlerFactory.CreateDefault(), new MutatorRegistry());
            Assert.IsNotNull(simple.Catalog);

            var built = new StoreBuilder().Build();
            Assert.IsNotNull(built.Catalog);
        }

        [Test]
        public void Snapshot_DoesNotContainCatalog()
        {
            // Structural assertion: Snapshot is keyed on (Reference, Type, State).
            // No public surface for catalog state on Snapshot. This test pins that
            // by registering a ref, taking a snapshot, and verifying the snapshot
            // contains zero entries (no slices added).
            var store = new StoreBuilder().Build();
            store.Catalog.Register(new Card("ace"));

            Snapshot snap = store.SaveSnapshot();

            Assert.AreEqual(0, snap.Count);
        }

        [Test]
        public void LoadSnapshot_DoesNotTouchCatalog()
        {
            var store = new StoreBuilder().Build();
            var first = new Card("first");
            var firstRef = store.Catalog.Register(first);
            Snapshot snap = store.SaveSnapshot();

            var second = new Card("second");
            var secondRef = store.Catalog.Register(second);

            store.LoadSnapshot(snap);

            Assert.AreSame(first, store.Catalog.Resolve(firstRef));
            Assert.AreSame(second, store.Catalog.Resolve(secondRef));
        }

        [Test]
        public void LoadSnapshot_ReverseDirection_RefsStillResolve()
        {
            var store = new StoreBuilder().Build();
            var card = new Card("ace");
            var cardRef = store.Catalog.Register(card);

            // Take the snapshot AFTER registration; rolling forward to it should not
            // un-register the ref.
            Snapshot snap = store.SaveSnapshot();
            store.LoadSnapshot(snap);

            Assert.AreSame(card, store.Catalog.Resolve(cardRef));
        }
    }
}

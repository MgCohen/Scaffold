#nullable enable
using System;
using NUnit.Framework;
using Scaffold.States;

namespace Scaffold.States.Tests
{
    public class CatalogIntegrationTests
    {
        private sealed class Card
        {
            public string Name { get; }
            public Card(string name) { Name = name; }
        }

        private sealed record CardOwnerState(Ref<Card> Owner) : State;

        private sealed record CardHealthState(int Health) : State;

        private sealed record DamageCardPayload(Ref<Card> Target, int Amount) : IPayloadReference
        {
            public Reference GetReference() => Target;
        }

        private sealed class ApplyDamageMutator : Mutator<CardHealthState, DamageCardPayload>
        {
            public override CardHealthState Change(CardHealthState state, DamageCardPayload payload, IStateScope scope)
            {
                return new CardHealthState(state.Health - payload.Amount);
            }
        }

        // Slice value containing a Ref<T> survives snapshot round-trip — the catalog
        // is unaffected, so the ref still resolves to the original object after load.
        [Test]
        public void SliceValueContainingRef_RoundTripsThroughSnapshot()
        {
            var store = new StoreBuilder().Build();
            var card = new Card("ace");
            var cardRef = store.Catalog.Register(card);

            store.RegisterSlice(Reference.Null, new CardOwnerState(cardRef));
            var snap = store.SaveSnapshot();

            store.UnregisterSlice<CardOwnerState>(Reference.Null);
            store.LoadSnapshot(snap);

            var restored = store.Get<CardOwnerState>();
            Assert.AreEqual(cardRef, restored.Owner);
            Assert.AreSame(card, store.Catalog.Resolve(restored.Owner));
        }

        // End-to-end: register two, slice keyed by first with second in its value,
        // snapshot, register a third afterward, load. Originals still resolve, third
        // still resolves (catalog isn't rolled back), but the third does not appear
        // in any slice.
        [Test]
        public void EndToEnd_RegisterTwoSnapshotRegisterThirdLoad_AllResolveThirdNotInSlices()
        {
            var store = new StoreBuilder().Build();
            var first = new Card("first");
            var second = new Card("second");
            var firstRef = store.Catalog.Register(first);
            var secondRef = store.Catalog.Register(second);

            store.RegisterSlice(firstRef, new CardOwnerState(secondRef));
            var snap = store.SaveSnapshot();

            var third = new Card("third");
            var thirdRef = store.Catalog.Register(third);

            store.LoadSnapshot(snap);

            Assert.AreSame(first, store.Catalog.Resolve(firstRef));
            Assert.AreSame(second, store.Catalog.Resolve(secondRef));
            Assert.AreSame(third, store.Catalog.Resolve(thirdRef));

            var restoredOwner = store.Get<CardOwnerState>(firstRef);
            Assert.AreEqual(secondRef, restoredOwner.Owner);

            Assert.IsFalse(store.TryGet<CardOwnerState>(thirdRef, out _));
        }

        // Payload with a Ref<T> Target field routes through Execute via
        // IPayloadReference.GetReference() to the slice keyed by that ref —
        // the IPayloadReference contract returns Reference, and Ref<T> : Reference.
        [Test]
        public void Payload_WithRefTarget_RoutesViaExecuteToKeyedSlice()
        {
            var builder = new StoreBuilder();
            builder.RegisterMutator(new ApplyDamageMutator());
            var store = builder.Build();

            var card = new Card("ace");
            var cardRef = store.Catalog.Register(card);
            store.RegisterSlice(cardRef, new CardHealthState(10));

            store.Execute(new DamageCardPayload(cardRef, 3));

            Assert.AreEqual(7, store.Get<CardHealthState>(cardRef).Health);
        }
    }
}

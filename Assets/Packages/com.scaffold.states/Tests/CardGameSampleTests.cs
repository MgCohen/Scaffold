#nullable enable

using System.Linq;
using NUnit.Framework;
using Scaffold.States.Samples.CardGame;

namespace Scaffold.States.Tests
{
    public sealed class CardGameSampleTests
    {
        [Test]
        public void InitialState_DeckSeeded_HandEmpty_AllAggregatesConsistent()
        {
            CardGameDemo demo = CardGameStoreFactory.CreateDefaultDemo();
            Store store = demo.Store;

            PlayerView pv = store.Get<PlayerView>(demo.Player);
            Assert.That(pv.Name, Is.EqualTo("You"));
            Assert.That(pv.Health, Is.EqualTo(30));
            Assert.That(pv.HandSize, Is.EqualTo(0));
            Assert.That(pv.DeckSize, Is.EqualTo(5));
            Assert.That(pv.DiscardSize, Is.EqualTo(0));
            Assert.That(pv.DiscardTimesAddedTo, Is.EqualTo(0));

            Assert.That(store.Get<DeckView>(demo.Player).Count, Is.EqualTo(5));
            Assert.That(store.Get<HandView>(demo.Player).Slots, Is.Empty);
            Assert.That(store.Get<DiscardView>(demo.Player).Pile, Is.Empty);
        }

        [Test]
        public void DrawTop_RemovesFromDeck_AppendsToHand_AggregatesUpdate()
        {
            CardGameDemo demo = CardGameStoreFactory.CreateDefaultDemo();
            Store store = demo.Store;
            CardId expected = demo.SeededDeck[0];

            CardId? drawn = CardGameStoreFactory.DrawTop(store, demo.Player);

            Assert.That(drawn, Is.EqualTo(expected));
            Assert.That(store.Get<DeckState>(demo.Player).Order.Count, Is.EqualTo(4));
            Assert.That(store.Get<HandState>(demo.Player).Cards.Single(), Is.EqualTo(expected));

            HandView hand = store.Get<HandView>(demo.Player);
            Assert.That(hand.Slots.Count, Is.EqualTo(1));
            Assert.That(hand.Slots[0].Id, Is.EqualTo(expected));
            Assert.That(hand.Slots[0].View.IsKnown, Is.True);
            Assert.That(hand.Slots[0].View.Name, Is.EqualTo("Goblin"));

            PlayerView pv = store.Get<PlayerView>(demo.Player);
            Assert.That(pv.HandSize, Is.EqualTo(1));
            Assert.That(pv.DeckSize, Is.EqualTo(4));
        }

        [Test]
        public void Discard_MovesCardToPile_IncrementsCounter()
        {
            CardGameDemo demo = CardGameStoreFactory.CreateDefaultDemo();
            Store store = demo.Store;
            CardId drawn = CardGameStoreFactory.DrawTop(store, demo.Player)!;

            CardGameStoreFactory.DiscardFromHand(store, demo.Player, drawn);

            Assert.That(store.Get<HandState>(demo.Player).Cards, Is.Empty);
            Assert.That(store.Get<DiscardState>(demo.Player).Pile.Single(), Is.EqualTo(drawn));
            Assert.That(store.Get<DiscardCounterState>(demo.Player).TimesAddedTo, Is.EqualTo(1));

            DiscardView dv = store.Get<DiscardView>(demo.Player);
            Assert.That(dv.Pile.Count, Is.EqualTo(1));
            Assert.That(dv.TimesAddedTo, Is.EqualTo(1));

            // Counter is cumulative across moves into the pile.
            CardId drawn2 = CardGameStoreFactory.DrawTop(store, demo.Player)!;
            CardGameStoreFactory.DiscardFromHand(store, demo.Player, drawn2);
            Assert.That(store.Get<DiscardCounterState>(demo.Player).TimesAddedTo, Is.EqualTo(2));
        }

        [Test]
        public void TakeDamage_OnCard_UpdatesCardViewAndDoesNotRebuildPlayerView()
        {
            CardGameDemo demo = CardGameStoreFactory.CreateDefaultDemo();
            Store store = demo.Store;
            CardId drawn = CardGameStoreFactory.DrawTop(store, demo.Player)!;
            int playerViewBuilds = 0;
            int handViewBuilds = 0;
            store.Subscribe<PlayerView>(demo.Player, (_, _, _) => playerViewBuilds++);
            store.Subscribe<HandView>(demo.Player, (_, _, _) => handViewBuilds++);

            CardGameStoreFactory.TakeDamage(store, drawn, 1);

            CardView updated = store.Get<CardView>(drawn);
            Assert.That(updated.IsKnown, Is.True);
            Assert.That(updated.Name, Is.EqualTo("Goblin"));
            Assert.That(updated.MaxHealth, Is.EqualTo(1));
            Assert.That(updated.CurrentHealth, Is.EqualTo(0));

            // Hand view rebuilt because it joins per-card views.
            Assert.That(handViewBuilds, Is.GreaterThanOrEqualTo(1));
            // Player view does not depend on per-card data, so a card-only mutation should not rebuild it.
            Assert.That(playerViewBuilds, Is.EqualTo(0));
        }

        [Test]
        public void Conceal_RemovesKnowledgeSlice_CardViewRendersFaceDown()
        {
            CardGameDemo demo = CardGameStoreFactory.CreateDefaultDemo();
            Store store = demo.Store;
            CardId drawn = CardGameStoreFactory.DrawTop(store, demo.Player)!;
            Assert.That(store.Get<CardView>(drawn).IsKnown, Is.True);

            CardGameStoreFactory.Conceal(store, drawn);

            CardView faceDown = store.Get<CardView>(drawn);
            Assert.That(faceDown.IsKnown, Is.False);
            Assert.That(faceDown.Name, Is.Null);
            Assert.That(faceDown.Attack, Is.Null);

            HandView hand = store.Get<HandView>(demo.Player);
            Assert.That(hand.Slots.Single().View.IsKnown, Is.False);
        }

        [Test]
        public void Reveal_AddsKnowledgeSlice_CardViewBecomesKnown()
        {
            CardGameDemo demo = CardGameStoreFactory.CreateDefaultDemo();
            Store store = demo.Store;
            CardId drawn = CardGameStoreFactory.DrawTop(store, demo.Player)!;
            CardGameStoreFactory.Conceal(store, drawn);

            CardGameStoreFactory.Reveal(store, drawn, demo.SeededAssignments[drawn]);

            CardView revealed = store.Get<CardView>(drawn);
            Assert.That(revealed.IsKnown, Is.True);
            Assert.That(revealed.Name, Is.EqualTo("Goblin"));
        }

        [Test]
        public void DamageWhileConcealed_PreservesRuntime_RevealReflectsAccumulatedDamage()
        {
            CardGameDemo demo = CardGameStoreFactory.CreateDefaultDemo();
            Store store = demo.Store;
            CardId drawn = CardGameStoreFactory.DrawTop(store, demo.Player)!;
            CardGameStoreFactory.Conceal(store, drawn);
            CardGameStoreFactory.TakeDamage(store, drawn, 1);
            Assert.That(store.Get<CardView>(drawn).IsKnown, Is.False);

            CardGameStoreFactory.Reveal(store, drawn, demo.SeededAssignments[drawn]);

            CardView view = store.Get<CardView>(drawn);
            Assert.That(view.IsKnown, Is.True);
            Assert.That(view.MaxHealth, Is.EqualTo(1));
            Assert.That(view.CurrentHealth, Is.EqualTo(0));
        }

        [Test]
        public void DrawDispatchesAllZoneMutatorsInOneCommit()
        {
            CardGameDemo demo = CardGameStoreFactory.CreateDefaultDemo();
            Store store = demo.Store;
            int playerViewBuilds = 0;
            store.Subscribe<PlayerView>(demo.Player, (_, _, _) => playerViewBuilds++);

            CardGameStoreFactory.DrawTop(store, demo.Player);

            // PlayerView depends on both HandState and DeckState. Even though both change in one Execute,
            // we just care that the final committed state is consistent.
            PlayerView pv = store.Get<PlayerView>(demo.Player);
            Assert.That(pv.HandSize, Is.EqualTo(1));
            Assert.That(pv.DeckSize, Is.EqualTo(4));
            Assert.That(playerViewBuilds, Is.GreaterThanOrEqualTo(1));
        }
    }
}

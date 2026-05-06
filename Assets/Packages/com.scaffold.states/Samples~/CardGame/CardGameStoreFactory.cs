#nullable enable

using System.Collections.Generic;

namespace Scaffold.States.Samples.CardGame
{
    public sealed record CardGameDemo(
        Store Store,
        CardCatalog Catalog,
        PlayerId Player,
        IReadOnlyList<CardId> SeededDeck,
        IReadOnlyDictionary<CardId, CardDefId> SeededAssignments);

    public static class CardGameStoreFactory
    {
        // Single-player demo. Five card instances seeded into the deck. All instances are "known" by default;
        // call Conceal/Reveal in tests to exercise the hidden-information path.
        public static CardGameDemo CreateDefaultDemo()
        {
            var catalog = new CardCatalog(new[]
            {
                new CardDef(new CardDefId(1), "Goblin", Cost: 1, Atk: 2, Hp: 1),
                new CardDef(new CardDefId(2), "Dragon", Cost: 5, Atk: 5, Hp: 5),
                new CardDef(new CardDefId(3), "Healer", Cost: 2, Atk: 1, Hp: 3),
            });

            var player = new PlayerId(1);

            var card1 = new CardId(101);
            var card2 = new CardId(102);
            var card3 = new CardId(103);
            var card4 = new CardId(104);
            var card5 = new CardId(105);

            var assignments = new Dictionary<CardId, CardDefId>
            {
                [card1] = new CardDefId(1),
                [card2] = new CardDefId(2),
                [card3] = new CardDefId(3),
                [card4] = new CardDefId(1),
                [card5] = new CardDefId(2),
            };

            var deckOrder = new List<CardId> { card1, card2, card3, card4, card5 };

            var builder = new StoreBuilder();

            // Player-keyed canonical state.
            builder.AddState(player, new PlayerCoreState("You", Health: 30, MaxHealth: 30));
            builder.AddState(player, new HandState(new List<CardId>()));
            builder.AddState(player, new DeckState(deckOrder));
            builder.AddState(player, new DiscardState(new List<CardId>()));
            builder.AddState(player, new DiscardCounterState(0));

            // Per-card canonical state. CardRuntimeState is the "this client is aware of the instance" presence flag.
            // CardKnowledgeState carries the weak ref to the catalog. Registered iff this client knows the def.
            foreach (CardId cid in deckOrder)
            {
                builder.AddState(cid, new CardRuntimeState(Damage: 0, BonusAtk: 0));
                builder.AddState(cid, new CardKnowledgeState(assignments[cid]));
            }

            // Aggregates registered AFTER all canonical state so initial Build calls succeed.
            foreach (CardId cid in deckOrder)
            {
                builder.RegisterAggregate(cid, new CardViewProvider(cid, catalog));
            }
            builder.RegisterAggregate(player, new HandViewProvider(player));
            builder.RegisterAggregate(player, new DeckViewProvider(player));
            builder.RegisterAggregate(player, new DiscardViewProvider(player));
            builder.RegisterAggregate(player, new PlayerViewProvider(player));

            // Mutators: one payload type binds N slice-typed mutators. Execute commits once.
            builder.RegisterMutator(new DrawCard_DeckMutator());
            builder.RegisterMutator(new DrawCard_HandMutator());
            builder.RegisterMutator(new DiscardFromHand_HandMutator());
            builder.RegisterMutator(new DiscardFromHand_DiscardMutator());
            builder.RegisterMutator(new DiscardFromHand_CounterMutator());
            builder.RegisterMutator(new TakeDamage_RuntimeMutator());

            Store store = builder.Build();
            return new CardGameDemo(store, catalog, player, deckOrder, assignments);
        }

        // Pulls the top of the deck and dispatches the multi-slice draw payload.
        public static CardId? DrawTop(Store store, PlayerId player)
        {
            DeckState deck = store.Get<DeckState>(player);
            if (deck.Order.Count == 0)
            {
                return null;
            }

            CardId top = deck.Order[0];
            store.Execute(new DrawCardPayload(player, top));
            return top;
        }

        public static bool DiscardFromHand(Store store, PlayerId player, CardId card)
        {
            HandState hand = store.Get<HandState>(player);
            bool inHand = false;
            for (int i = 0; i < hand.Cards.Count; i++)
            {
                if (hand.Cards[i].Equals(card))
                {
                    inHand = true;
                    break;
                }
            }

            if (!inHand)
            {
                return false;
            }

            store.Execute(new DiscardFromHandPayload(player, card));
            return true;
        }

        public static void TakeDamage(Store store, CardId card, int amount)
        {
            store.Execute(new TakeDamagePayload(card, amount));
        }

        // Reveal/Conceal model the network case where a card's identity is shown or hidden.
        // They directly add/remove the CardKnowledgeState slice; CardView rebuilds via its subscription.
        public static void Reveal(Store store, CardId card, CardDefId def)
        {
            store.RegisterSlice(card, new CardKnowledgeState(def));
        }

        public static void Conceal(Store store, CardId card)
        {
            store.UnregisterSlice<CardKnowledgeState>(card);
        }
    }
}

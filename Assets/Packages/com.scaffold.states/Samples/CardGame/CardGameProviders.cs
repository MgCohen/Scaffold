#nullable enable

using System.Collections.Generic;

namespace Scaffold.States.Samples.CardGame
{
    public sealed class CardViewProvider : AggregateProvider<CardView>
    {
        private readonly CardId id;
        private readonly CardCatalog catalog;

        public CardViewProvider(CardId id, CardCatalog catalog)
        {
            this.id = id;
            this.catalog = catalog;
        }

        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            scope.Events.Subscribe<CardKnowledgeState>(id, (_, _, _) => rebuild.RequestRebuild());
            scope.Events.Subscribe<CardRuntimeState>(id, (_, _, _) => rebuild.RequestRebuild());
        }

        protected override CardView BuildCore(IStateScope scope)
        {
            CardRuntimeState runtime = scope.Get<CardRuntimeState>(id);

            CardKnowledgeState? knowledge = TryGet<CardKnowledgeState>(scope, id);
            if (knowledge is null)
            {
                return new CardView(id, IsKnown: false, Name: null, Cost: null, Attack: null, CurrentHealth: null, MaxHealth: null);
            }

            CardDef def = catalog.Get(knowledge.Def);
            int attack = def.Atk + runtime.BonusAtk;
            int max = def.Hp;
            int current = max - runtime.Damage;
            return new CardView(id, IsKnown: true, def.Name, def.Cost, attack, current, max);
        }

        private static T? TryGet<T>(IStateScope scope, IReference reference) where T : BaseState
        {
            try
            {
                return scope.Get<T>(reference);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }

    public sealed class HandViewProvider : AggregateProvider<HandView>
    {
        private readonly PlayerId player;

        public HandViewProvider(PlayerId player)
        {
            this.player = player;
        }

        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            scope.Events.Subscribe<HandState>(player, (_, _, _) => rebuild.RequestRebuild());
            // Any per-card view change affects whatever hand currently holds it. Cheap join, broad subscribe.
            scope.Events.SubscribeAllReferences<CardView>((_, _, _) => rebuild.RequestRebuild());
        }

        protected override HandView BuildCore(IStateScope scope)
        {
            HandState hand = scope.Get<HandState>(player);
            var slots = new List<HandSlot>(hand.Cards.Count);
            for (int i = 0; i < hand.Cards.Count; i++)
            {
                CardId cid = hand.Cards[i];
                CardView view = scope.Get<CardView>(cid);
                slots.Add(new HandSlot(cid, view));
            }
            return new HandView(slots);
        }
    }

    public sealed class DeckViewProvider : AggregateProvider<DeckView>
    {
        private readonly PlayerId player;

        public DeckViewProvider(PlayerId player)
        {
            this.player = player;
        }

        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            scope.Events.Subscribe<DeckState>(player, (_, _, _) => rebuild.RequestRebuild());
        }

        protected override DeckView BuildCore(IStateScope scope)
        {
            DeckState deck = scope.Get<DeckState>(player);
            return new DeckView(deck.Order.Count);
        }
    }

    public sealed class DiscardViewProvider : AggregateProvider<DiscardView>
    {
        private readonly PlayerId player;

        public DiscardViewProvider(PlayerId player)
        {
            this.player = player;
        }

        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            scope.Events.Subscribe<DiscardState>(player, (_, _, _) => rebuild.RequestRebuild());
            scope.Events.Subscribe<DiscardCounterState>(player, (_, _, _) => rebuild.RequestRebuild());
            scope.Events.SubscribeAllReferences<CardView>((_, _, _) => rebuild.RequestRebuild());
        }

        protected override DiscardView BuildCore(IStateScope scope)
        {
            DiscardState pile = scope.Get<DiscardState>(player);
            DiscardCounterState counter = scope.Get<DiscardCounterState>(player);
            var slots = new List<HandSlot>(pile.Pile.Count);
            for (int i = 0; i < pile.Pile.Count; i++)
            {
                CardId cid = pile.Pile[i];
                CardView view = scope.Get<CardView>(cid);
                slots.Add(new HandSlot(cid, view));
            }
            return new DiscardView(slots, counter.TimesAddedTo);
        }
    }

    public sealed class PlayerViewProvider : AggregateProvider<PlayerView>
    {
        private readonly PlayerId player;

        public PlayerViewProvider(PlayerId player)
        {
            this.player = player;
        }

        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            scope.Events.Subscribe<PlayerCoreState>(player, (_, _, _) => rebuild.RequestRebuild());
            scope.Events.Subscribe<HandState>(player, (_, _, _) => rebuild.RequestRebuild());
            scope.Events.Subscribe<DeckState>(player, (_, _, _) => rebuild.RequestRebuild());
            scope.Events.Subscribe<DiscardState>(player, (_, _, _) => rebuild.RequestRebuild());
            scope.Events.Subscribe<DiscardCounterState>(player, (_, _, _) => rebuild.RequestRebuild());
        }

        protected override PlayerView BuildCore(IStateScope scope)
        {
            PlayerCoreState core = scope.Get<PlayerCoreState>(player);
            HandState hand = scope.Get<HandState>(player);
            DeckState deck = scope.Get<DeckState>(player);
            DiscardState discard = scope.Get<DiscardState>(player);
            DiscardCounterState counter = scope.Get<DiscardCounterState>(player);

            return new PlayerView(
                core.Name,
                core.Health,
                core.MaxHealth,
                hand.Cards.Count,
                deck.Order.Count,
                discard.Pile.Count,
                counter.TimesAddedTo,
                hand.Cards,
                discard.Pile);
        }
    }
}

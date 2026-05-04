#nullable enable

using System.Collections.Generic;

namespace Scaffold.States.Samples.CardGame
{
    public sealed class DrawCard_DeckMutator : Mutator<DeckState, DrawCardPayload>
    {
        public override DeckState Change(DeckState state, DrawCardPayload payload, IStateScope scope)
        {
            if (state.Order.Count == 0)
            {
                return state;
            }

            var next = new List<CardId>(state.Order.Count);
            for (int i = 0; i < state.Order.Count; i++)
            {
                if (!state.Order[i].Equals(payload.Card))
                {
                    next.Add(state.Order[i]);
                }
            }

            if (next.Count == state.Order.Count)
            {
                return state;
            }

            return new DeckState(next);
        }
    }

    public sealed class DrawCard_HandMutator : Mutator<HandState, DrawCardPayload>
    {
        public override HandState Change(HandState state, DrawCardPayload payload, IStateScope scope)
        {
            var next = new List<CardId>(state.Cards.Count + 1);
            for (int i = 0; i < state.Cards.Count; i++)
            {
                next.Add(state.Cards[i]);
            }
            next.Add(payload.Card);
            return new HandState(next);
        }
    }

    public sealed class DiscardFromHand_HandMutator : Mutator<HandState, DiscardFromHandPayload>
    {
        public override HandState Change(HandState state, DiscardFromHandPayload payload, IStateScope scope)
        {
            var next = new List<CardId>(state.Cards.Count);
            bool removed = false;
            for (int i = 0; i < state.Cards.Count; i++)
            {
                if (!removed && state.Cards[i].Equals(payload.Card))
                {
                    removed = true;
                    continue;
                }
                next.Add(state.Cards[i]);
            }

            if (!removed)
            {
                return state;
            }

            return new HandState(next);
        }
    }

    // Mutators trust the payload; the dispatch site validates that the card is actually in the player's hand.
    public sealed class DiscardFromHand_DiscardMutator : Mutator<DiscardState, DiscardFromHandPayload>
    {
        public override DiscardState Change(DiscardState state, DiscardFromHandPayload payload, IStateScope scope)
        {
            var next = new List<CardId>(state.Pile.Count + 1);
            for (int i = 0; i < state.Pile.Count; i++)
            {
                next.Add(state.Pile[i]);
            }
            next.Add(payload.Card);
            return new DiscardState(next);
        }
    }

    public sealed class DiscardFromHand_CounterMutator : Mutator<DiscardCounterState, DiscardFromHandPayload>
    {
        public override DiscardCounterState Change(DiscardCounterState state, DiscardFromHandPayload payload, IStateScope scope)
        {
            return new DiscardCounterState(state.TimesAddedTo + 1);
        }
    }

    public sealed class TakeDamage_RuntimeMutator : Mutator<CardRuntimeState, TakeDamagePayload>
    {
        public override CardRuntimeState Change(CardRuntimeState state, TakeDamagePayload payload, IStateScope scope)
        {
            return new CardRuntimeState(state.Damage + payload.Damage, state.BonusAtk);
        }
    }
}

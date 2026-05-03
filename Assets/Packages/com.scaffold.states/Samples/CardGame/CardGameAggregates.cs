#nullable enable

using System.Collections.Generic;

namespace Scaffold.States.Samples.CardGame
{
    // Per-card aggregate. Joins CardKnowledgeState (optional) + CardRuntimeState + Catalog.
    // IsKnown == false means this client has the instance id but does not know which card it is.
    public sealed record CardView(
        CardId Id,
        bool IsKnown,
        string? Name,
        int? Cost,
        int? Attack,
        int? CurrentHealth,
        int? MaxHealth) : AggregateState;

    // Per-player. Joins HandState ids with each card's CardView.
    public sealed record HandSlot(CardId Id, CardView View);

    public sealed record HandView(IReadOnlyList<HandSlot> Slots) : AggregateState;

    // Per-player. Deck cards are face-down to the player by convention; expose only the count.
    public sealed record DeckView(int Count) : AggregateState;

    // Per-player. Joins DiscardState + DiscardCounterState + each card's CardView.
    public sealed record DiscardView(
        IReadOnlyList<HandSlot> Pile,
        int TimesAddedTo) : AggregateState;

    // Per-player top-level dashboard: scalars + zone sizes + ids for cell-level binding.
    public sealed record PlayerView(
        string Name,
        int Health,
        int MaxHealth,
        int HandSize,
        int DeckSize,
        int DiscardSize,
        int DiscardTimesAddedTo,
        IReadOnlyList<CardId> HandCardIds,
        IReadOnlyList<CardId> DiscardCardIds) : AggregateState;
}

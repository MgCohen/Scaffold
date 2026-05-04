#nullable enable

using System.Collections.Generic;

namespace Scaffold.States.Samples.CardGame
{
    // Per-card state, keyed by CardId. Registered iff this client knows the instance exists.
    public sealed record CardRuntimeState(int Damage, int BonusAtk) : State;

    // Per-card state, keyed by CardId. Registered iff this client has been told which definition this instance is.
    // Missing slice = face-down to this client.
    public sealed record CardKnowledgeState(CardDefId Def) : State;

    // Per-player state, keyed by PlayerId.
    public sealed record PlayerCoreState(string Name, int Health, int MaxHealth) : State;

    // Per-player zone slices, all keyed by PlayerId. Each list holds CardIds; cards are not duplicated.
    public sealed record HandState(IReadOnlyList<CardId> Cards) : State;

    public sealed record DeckState(IReadOnlyList<CardId> Order) : State;

    public sealed record DiscardState(IReadOnlyList<CardId> Pile) : State;

    // Custom zone state: counts how many cards have ever entered the discard pile.
    public sealed record DiscardCounterState(int TimesAddedTo) : State;
}

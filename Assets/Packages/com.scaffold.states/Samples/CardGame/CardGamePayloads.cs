#nullable enable

namespace Scaffold.States.Samples.CardGame
{
    // Routes via the player ref so player-keyed mutators execute against the right slices.
    public sealed record DrawCardPayload(PlayerId Player, CardId Card) : IPayloadReference
    {
        public IReference GetReference() => Player;
    }

    public sealed record DiscardFromHandPayload(PlayerId Player, CardId Card) : IPayloadReference
    {
        public IReference GetReference() => Player;
    }

    // Routes to the card itself; CardRuntimeState is keyed by CardId.
    public sealed record TakeDamagePayload(CardId Card, int Damage) : IPayloadReference
    {
        public IReference GetReference() => Card;
    }
}

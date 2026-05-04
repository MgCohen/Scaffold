#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Fired by <c>DealDamageCommand</c> before damage is applied. Trigger entries on cards may
    /// mutate <see cref="Amount"/> in flight (e.g. PlusOneDamage adds 1).
    /// </summary>
    [GraphEntry]
    public sealed class PreDamageDealtEvent : IGraphEntry
    {
        [GraphPort]
        public int Amount;

        public object? Target;
    }

    /// <summary>Fired after damage was applied; reactive triggers ("when damage dealt, draw a card") subscribe here.</summary>
    [GraphEntry]
    public sealed class DamageDealtEvent : IGraphEntry
    {
        [GraphPort]
        public int FinalAmount;

        public object? Target;
    }

    /// <summary>OnPlay entry payload for cards. Imperative entry — host calls it directly via Run.</summary>
    [GraphEntry]
    public sealed class OnPlay : IGraphEntry
    {
        public object? Target;
    }
}

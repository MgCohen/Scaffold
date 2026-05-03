#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Fired by <c>DealDamageCommand</c> before damage is applied. Trigger entries on cards may
    /// mutate <see cref="Amount"/> in flight (e.g. PlusOneDamage adds 1).
    /// <para>The <see cref="IGraphTrigger{TEvent}"/> marker tells the host this type is intended to
    /// be subscribed by trigger entries. The class is its own payload.</para>
    /// </summary>
    public sealed class PreDamageDealtEvent : IGraphTrigger<PreDamageDealtEvent>
    {
        public int Amount;
        public object? Target;
    }

    /// <summary>Fired after damage was applied; reactive triggers ("when damage dealt, draw a card") subscribe here.</summary>
    public sealed class DamageDealtEvent : IGraphTrigger<DamageDealtEvent>
    {
        public int FinalAmount;
        public object? Target;
    }

    /// <summary>OnPlay entry payload for cards. Imperative entry — host calls it directly via Run.</summary>
    public sealed class OnPlay : IGraphEntry<OnPlay>
    {
        public object? Target;
    }
}

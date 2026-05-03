#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Fired by <c>DealDamageCommand</c> before damage is applied. Trigger entries on cards may
    /// mutate <see cref="Amount"/> in flight (e.g. PlusOneDamage adds 1).
    /// <para>The <see cref="IGraphTrigger{TEvent}"/> marker tells the host this type is intended to
    /// be subscribed by trigger entries. The class is its own payload.</para>
    /// <para><see cref="GraphEntryAttribute"/> opts the type into the generator so an editor mirror
    /// (<c>PreDamageDealtEventEditorNode</c>) and runtime (<c>PreDamageDealtEventRuntime</c>) get
    /// emitted. Triggers and entries share the same node shape (D5).</para>
    /// </summary>
    [GraphEntry(FlowOutPortId = unchecked((int)0xCE00_0001u))]
    public sealed class PreDamageDealtEvent : IGraphTrigger<PreDamageDealtEvent>
    {
        [GraphPort(Id = unchecked((int)0xCE00_1001u))]
        public int Amount;

        public object? Target;
    }

    /// <summary>Fired after damage was applied; reactive triggers ("when damage dealt, draw a card") subscribe here.</summary>
    [GraphEntry(FlowOutPortId = unchecked((int)0xCE00_0002u))]
    public sealed class DamageDealtEvent : IGraphTrigger<DamageDealtEvent>
    {
        [GraphPort(Id = unchecked((int)0xCE00_1002u))]
        public int FinalAmount;

        public object? Target;
    }

    /// <summary>OnPlay entry payload for cards. Imperative entry — host calls it directly via Run.</summary>
    [GraphEntry(FlowOutPortId = unchecked((int)0xCE00_0003u))]
    public sealed class OnPlay : IGraphEntry<OnPlay>
    {
        public object? Target;
    }
}

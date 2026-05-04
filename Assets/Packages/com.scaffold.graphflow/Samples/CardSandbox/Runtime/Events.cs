#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Damage event — published by <c>DealDamageCommand</c> twice per damage application: once with
    /// <see cref="Timing.Before"/> (so triggers may mutate <see cref="Amount"/>) and once with
    /// <see cref="Timing.After"/> (reactive triggers).
    /// </summary>
    [GraphEvent]
    public sealed class DamageDealt
    {
        public int Amount;
        public object? Target;
    }

    /// <summary>OnPlay imperative entry payload for cards. Host calls it directly via <c>controller.Run</c>.</summary>
    [GraphEntry]
    public sealed class OnPlay : IGraphEntry
    {
        public object? Target;
    }
}

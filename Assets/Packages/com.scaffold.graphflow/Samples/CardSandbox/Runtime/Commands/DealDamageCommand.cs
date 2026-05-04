#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>Mode-2 command — applies damage and brackets it with Pre/Post DamageDealt events.</summary>
    public sealed class DealDamageCommand : Command<Unit>
    {
        [GraphPort]
        public int Amount;

        public object? Target;

        public override async Task<Unit> Execute(ICardEffectScope scope, Flow flow)
        {
            var evt = new DamageDealt { Amount = Amount, Target = Target };
            await scope.Bus.Publish(evt, Timing.Before).ConfigureAwait(false);
            scope.Damage.Apply(evt.Target, evt.Amount);
            await scope.Bus.Publish(evt, Timing.After).ConfigureAwait(false);
            return Unit.Default;
        }
    }
}

#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Mode-2 command base. Subclasses carry typed input fields and produce a typed result.
    /// Executes directly against the per-run <see cref="ICardEffectScope"/> — no wrapping pipeline,
    /// no listener chain. Cross-card command modification happens via events + trigger entries on
    /// the host's <see cref="EventBus"/>, not here.
    /// </summary>
    public abstract class Command<TResult>
    {
        public abstract Task<TResult> Execute(ICardEffectScope scope, Flow flow);
    }
}

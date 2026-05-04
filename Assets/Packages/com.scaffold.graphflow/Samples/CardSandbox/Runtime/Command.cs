#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>Sandbox-local void-result placeholder for <c>Command&lt;Unit&gt;</c>. Replaces the
    /// framework-shipped <c>Unit</c> that was removed when entry TResult was dropped (post-M3 phase 1).</summary>
    public readonly struct Unit { public static readonly Unit Default = default; }

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

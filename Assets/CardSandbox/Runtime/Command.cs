#nullable enable
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Mode-2 command base. Subclasses carry typed input fields (e.g. <c>Damage</c>) and produce
    /// a typed result (e.g. <c>DamageResult</c>) when executed via the <see cref="CommandPipeline"/>.
    ///
    /// <para>The graph dispatcher node (<see cref="CardCommandDispatcher{TCmd, TResult}"/>) constructs
    /// the cmd, hands it to the runner's pipeline, then writes the result fields back onto the node's
    /// output ports. Listeners on the pipeline can rewrite the result before it returns to the graph.</para>
    /// </summary>
    public abstract class Command<TResult>
    {
        /// <summary>Produce the baseline result before any listeners run. Listeners post-process it.</summary>
        public abstract Task<TResult> Execute(IEffectScope scope);
    }
}

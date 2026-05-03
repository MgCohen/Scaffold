#nullable enable
using System.Threading.Tasks;

using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Pre/post hook registered on the <see cref="CommandPipeline"/>. The chain is built from outermost
    /// to innermost; each listener calls <paramref name="next"/> to delegate to the inner chain (which
    /// terminates at <see cref="Command{TResult}.Execute"/>) and may rewrite the returned result before
    /// returning it.
    ///
    /// <para>This is the single behavioural seam that lets cards modify each other's commands —
    /// e.g. a "+1 damage on first strike" listener intercepts <c>DealDamageCommand</c> and bumps
    /// the result.</para>
    /// </summary>
    public interface ICommandListener<in TCmd, TResult>
        where TCmd : Command<TResult>
    {
        Task<TResult> Intercept(TCmd command, IEffectScope scope, CommandNext<TResult> next);
    }

    /// <summary>Continuation handed to <see cref="ICommandListener{TCmd, TResult}.Intercept"/>.</summary>
    public delegate Task<TResult> CommandNext<TResult>();
}

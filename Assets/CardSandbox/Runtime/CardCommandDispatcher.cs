#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;

using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Mode-2 dispatcher base for card-effect graphs. Mirrors <c>MyDispatcherBase</c> from
    /// the GraphFlow M0 sandbox but routes through <see cref="CardEffectRunner.Send{TCmd, TResult}"/>
    /// instead of an inline switch — letting <see cref="CommandPipeline"/> listeners modify the
    /// result before the graph reads it.
    ///
    /// <para>Subclasses (one per <see cref="Command{TResult}"/>) override <see cref="BuildPayload"/>
    /// to construct <typeparamref name="TCmd"/> from the node's typed input ports, and
    /// <see cref="WriteOutputs"/> to publish <typeparamref name="TResult"/> on the output ports.
    /// In the full M3 generator pass, both halves are emitted from <c>[GraphCommandPair]</c> on the
    /// payload — exactly as <c>EchoDispatcherRuntime</c>'s emit shape.</para>
    /// </summary>
    public abstract class CardCommandDispatcher<TCmd, TResult> : RuntimeNode<CardEffectRunner>
        where TCmd : Command<TResult>
    {
        /// <summary>Port id of the single flow-out — generator stamps this; hand-written nodes pass it explicitly.</summary>
        protected abstract int FlowOutPortId { get; }

        protected abstract TCmd BuildPayload(CardEffectRunner runner);
        protected abstract void WriteOutputs(TResult result);

        public sealed override async Task<FlowContinuation> Execute(CardEffectRunner runner)
        {
            var cmd = BuildPayload(runner);
            var result = await runner.Send<TCmd, TResult>(cmd).ConfigureAwait(false);
            WriteOutputs(result);
            return FlowContinuation.Next(FlowOutPortId);
        }
    }
}

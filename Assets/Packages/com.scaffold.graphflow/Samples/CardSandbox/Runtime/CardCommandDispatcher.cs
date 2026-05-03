#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Base for generator-emitted Mode-2 dispatcher runtimes. The generator closes this per
    /// <see cref="Command{TResult}"/> subclass it finds in the CardSandbox asm and fills in
    /// <c>BuildPayload</c> / <c>WriteOutputs</c> from the command's input/output port handles.
    /// Body here downcasts <see cref="Flow.Scope"/> to <see cref="ICardEffectScope"/>, awaits the
    /// command, writes outputs, then walks to <c>FlowOut</c>.
    /// <para>Mirrors <c>MyDispatcherBase&lt;,&gt;</c> in the M0 sandbox; the generator's command-pair
    /// emit calls <c>protected override int FlowOutPortId =&gt; ...</c> on the closed subclass and
    /// <c>protected override TCmd BuildPayload() =&gt; new TCmd { ... }</c> + a
    /// <c>protected override void WriteOutputs(TResult)</c>. Keep the override surface in sync with
    /// <c>EmitCommandRuntime</c> in <c>GraphPayloadNodeEmitter</c>.</para>
    /// </summary>
    public abstract class CardCommandDispatcher<TCmd, TResult> : RuntimeNode<CardEffectRunner>
        where TCmd : Command<TResult>, new()
    {
        protected abstract int FlowOutPortId { get; }
        protected abstract TCmd BuildPayload();
        protected abstract void WriteOutputs(TResult result);

        public sealed override async Task Execute(CardEffectRunner runner, Flow flow)
        {
            var scope = (ICardEffectScope)flow.Scope!;
            var cmd = BuildPayload();
            var result = await cmd.Execute(scope, flow).ConfigureAwait(false);
            WriteOutputs(result);
            await flow.GoTo(FlowOutPortId).ConfigureAwait(false);
        }
    }
}

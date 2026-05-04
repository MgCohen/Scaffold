#nullable enable
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Base for generator-emitted Mode-2 dispatcher runtimes. The generator closes this per
    /// <see cref="Command{TResult}"/> subclass it finds in the CardSandbox asm and fills in
    /// <c>BuildPayload</c> / <c>WriteOutputs</c> from the command's input/output port handles.
    /// <para>Owns the canonical flow ports — every command dispatcher has one FlowIn + one FlowOut,
    /// so they're declared here once and inherited by every generated subclass.</para>
    /// </summary>
    public abstract class CardCommandDispatcher<TCmd, TResult> : RuntimeNode<CardEffectRunner>
        where TCmd : Command<TResult>, new()
    {
        public FlowInPort FlowIn = null!;
        public FlowOutPort FlowOut = null!;

        protected CardCommandDispatcher()
        {
            FlowIn = new FlowInPort(this);
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(FlowOut.Name, FlowOut);
        }

        protected abstract TCmd BuildPayload();
        protected abstract void WriteOutputs(TResult result);

        public sealed override async Task Execute(CardEffectRunner runner, Flow flow)
        {
            var scope = (ICardEffectScope)flow.Scope!;
            var cmd = BuildPayload();
            var result = await cmd.Execute(scope, flow).ConfigureAwait(false);
            WriteOutputs(result);
            await flow.GoTo(FlowOut).ConfigureAwait(false);
        }
    }
}

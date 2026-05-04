#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Base for generator-emitted Mode-2 dispatcher runtimes. The generator closes this per
    /// <see cref="Command{TResult}"/> subclass it finds in the CardSandbox asm and fills in
    /// <c>BuildPayload</c> / <c>WriteOutputs</c> from the command's input/output port handles.
    /// </summary>
    public abstract class CardCommandDispatcher<TCmd, TResult> : RuntimeNode<CardEffectRunner>
        where TCmd : Command<TResult>, new()
    {
        protected abstract string FlowOutPortName { get; }
        protected abstract TCmd BuildPayload();
        protected abstract void WriteOutputs(TResult result);

        public sealed override async Task Execute(CardEffectRunner runner, Flow flow)
        {
            var scope = (ICardEffectScope)flow.Scope!;
            var cmd = BuildPayload();
            var result = await cmd.Execute(scope, flow).ConfigureAwait(false);
            WriteOutputs(result);
            await flow.GoTo(FlowOutPortName).ConfigureAwait(false);
        }
    }
}

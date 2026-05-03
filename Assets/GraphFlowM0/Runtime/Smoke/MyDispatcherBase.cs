using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>M0 stand-in for Card Framework <c>DispatcherBase</c> — Mode 2 emission shape.</summary>
    public abstract class MyDispatcherBase<TCmd, TResult> : RuntimeNode<MySmokeRunner>
        where TCmd : new()
    {
        /// <summary>Flow output after dispatch — matches editor <c>FlowOut</c> / baker.</summary>
        protected abstract int FlowOutPortId { get; }

        protected sealed override async ValueTask<FlowContinuation> Execute(MySmokeRunner runner)
        {
            var cmd = BuildPayload();
            var result = await DispatchAsync(runner, cmd).ConfigureAwait(false);
            WriteOutputs(result);
            return FlowContinuation.Next(FlowOutPortId);
        }

        protected abstract TCmd BuildPayload();
        protected abstract void WriteOutputs(TResult result);

        /// <summary>Fake Card Framework pipeline — replace with real dispatch in product code.</summary>
        protected virtual ValueTask<TResult> DispatchAsync(MySmokeRunner runner, TCmd cmd) =>
            throw new System.NotImplementedException();
    }
}

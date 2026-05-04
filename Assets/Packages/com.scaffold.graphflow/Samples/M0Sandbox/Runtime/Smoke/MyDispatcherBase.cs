using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>M0 stand-in for Card Framework <c>DispatcherBase</c> — Mode 2 emission shape.</summary>
    public abstract class MyDispatcherBase<TCmd, TResult> : RuntimeNode<MySmokeRunner>
        where TCmd : new()
    {
        /// <summary>Flow output after dispatch — matches editor <c>FlowOut</c> / baker.</summary>
        protected abstract string FlowOutPortName { get; }

        public sealed override async Task Execute(MySmokeRunner runner, Flow flow)
        {
            var cmd = BuildPayload();
            var result = await DispatchAsync(runner, cmd).ConfigureAwait(false);
            WriteOutputs(result);
            await flow.GoTo(FlowOutPortName);
        }

        protected abstract TCmd BuildPayload();
        protected abstract void WriteOutputs(TResult result);

        /// <summary>Fake Card Framework pipeline — replace with real dispatch in product code.</summary>
        protected virtual Task<TResult> DispatchAsync(MySmokeRunner runner, TCmd cmd)
        {
            throw new System.NotImplementedException();
        }
    }
}

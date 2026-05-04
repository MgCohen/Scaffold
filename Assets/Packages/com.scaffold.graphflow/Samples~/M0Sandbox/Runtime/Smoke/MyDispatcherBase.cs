using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>M0 stand-in for Card Framework <c>DispatcherBase</c> — Mode 2 emission shape.</summary>
    public abstract class MyDispatcherBase<TCmd, TResult> : RuntimeNode<MySmokeRunner>
        where TCmd : new()
    {
        public FlowInPort FlowIn = null!;
        public FlowOutPort FlowOut = null!;

        protected MyDispatcherBase()
        {
            FlowIn = new FlowInPort(this);
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(FlowOut.Name, FlowOut);
        }

        public sealed override async Task Execute(MySmokeRunner runner, Flow flow)
        {
            var cmd = BuildPayload();
            var result = await DispatchAsync(runner, cmd).ConfigureAwait(false);
            WriteOutputs(result);
            await flow.GoTo(FlowOut);
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

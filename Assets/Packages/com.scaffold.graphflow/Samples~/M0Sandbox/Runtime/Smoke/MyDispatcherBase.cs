using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    public abstract class MyDispatcherBase<TCmd, TResult> : RuntimeNode<MySmokeRunner>
        where TCmd : new()
    {
        public FlowInPort FlowIn = null!;
        public FlowOutPort FlowOut = null!;

        protected MyDispatcherBase()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            FlowIn = FlowInPort.Async(this, nameof(FlowIn), async flow =>
            {
                var cmd = BuildPayload(flow);
                var result = await DispatchAsync(Runner(flow), cmd).ConfigureAwait(false);
                WriteOutputs(flow, result);
                return FlowOut;
            });
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(FlowOut.Name, FlowOut);
        }

        protected abstract TCmd BuildPayload(Flow flow);
        protected abstract void WriteOutputs(Flow flow, TResult result);

        protected virtual Task<TResult> DispatchAsync(MySmokeRunner runner, TCmd cmd)
        {
            throw new System.NotImplementedException();
        }
    }
}

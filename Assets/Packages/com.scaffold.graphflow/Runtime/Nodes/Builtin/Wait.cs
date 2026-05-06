using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Time")]
    public sealed partial class Wait : RuntimeNode
    {
        public InputPort<float> Seconds = null!;
        public FlowInPort In = null!;
        public FlowOutPort Out = null!;

        partial void InitializePorts() =>
            In = FlowInPort.Async(this, nameof(In), async flow =>
            {
                await Task.Delay(TimeSpan.FromSeconds(Seconds.Read(flow)), flow.Token);
                return Out;
            });
    }
}

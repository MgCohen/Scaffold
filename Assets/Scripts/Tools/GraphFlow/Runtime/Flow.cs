using System.Collections.Generic;
using System.Threading;

namespace Scaffold.GraphFlow
{
    public sealed class Flow
    {
        public Flow(CancellationToken cancellation, Flow parent = null)
        {
            Cancellation = cancellation;
            Parent = parent;
        }

        public Flow Parent { get; }
        public CancellationToken Cancellation { get; }
        public GraphBlackboard Blackboard { get; } = new GraphBlackboard();
        public ExecutableNode CurrentNode { get; set; }
        public object ReactivePayload { get; set; }

        public object LastExecutedInstance { get; set; }

        public Dictionary<ExecutableNode, object> LastInstanceByNode { get; } = new Dictionary<ExecutableNode, object>();

        public INodeExecutorRegistry Registry { get; set; }

        public GraphRunner ActiveRunner { get; set; }

        public Flow CreateChild()
        {
            var c = new Flow(Cancellation, this)
            {
                Registry = Registry,
                ActiveRunner = ActiveRunner
            };
            foreach (var kv in Blackboard.Enumerate())
                c.Blackboard.Set(kv.Key, kv.Value);
            return c;
        }
    }

}

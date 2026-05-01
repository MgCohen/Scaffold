using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Sample
{
    public sealed partial class GraphFlowEntryPassThroughDefinition : GraphNodeDefinitionBase
    {
        public FlowInput In;
        public FlowOutput Out;

        public override ValueTask ExecuteAsync(Flow flow, CancellationToken cancellationToken) => default;
    }
}

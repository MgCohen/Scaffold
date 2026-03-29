using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Sample
{
    public sealed partial class InvokeSubGraphDefinition : GraphNodeDefinitionBase
    {
        public FlowInput In;
        public FlowOutput Out;

        public override async ValueTask ExecuteAsync(Flow flow, CancellationToken cancellationToken)
        {
            var nested = flow.CurrentNode?.NestedRuntimeGraph;
            if (nested == null || flow.ActiveRunner == null || flow.Registry == null)
                return;

            var child = flow.CreateChild();
            await flow.ActiveRunner.RunChildGraphAsync(
                nested.BuildExecutable(flow.Registry),
                new SubGraphEntry(),
                child,
                cancellationToken).ConfigureAwait(false);
        }
    }
}

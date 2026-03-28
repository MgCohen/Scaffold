using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Sample
{
    public sealed partial class MultiplyAddInputsDefinition : GraphNodeDefinitionBase
    {
        public FlowInput In;
        public FlowOutput Out;

        public override ValueTask ExecuteAsync(Flow flow, CancellationToken cancellationToken)
        {
            if (flow.ReactivePayload is AddNumbersInstance add)
            {
                add.A *= 2;
                add.B *= 2;
            }

            return default;
        }
    }
}

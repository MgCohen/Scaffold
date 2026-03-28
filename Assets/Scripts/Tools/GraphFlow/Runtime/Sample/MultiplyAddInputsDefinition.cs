using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Sample
{
    public sealed class MultiplyAddInputsDefinition : GraphNodeDefinitionBase
    {
        public FlowInput In;
        public FlowOutput Out;

        public override string DefinitionTypeId => GraphFlowDefinitionIds.MultiplyAddInputs;

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

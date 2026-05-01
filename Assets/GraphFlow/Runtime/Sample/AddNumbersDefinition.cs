using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Sample
{
    public sealed class AddNumbersInstance
    {
        public int A;
        public int B;
        public int Sum;
    }

    public sealed partial class AddNumbersDefinition : GraphNodeDefinitionBase<AddNumbersInstance>
    {
        public InputConnection<int> A;
        public InputConnection<int> B;
        public OutputConnection<int> Sum;
        public FlowInput In;
        public FlowOutput Out;

        protected override ValueTask ExecuteAsync(AddNumbersInstance instance, Flow flow, CancellationToken cancellationToken)
        {
            instance.Sum = instance.A + instance.B;
            return default;
        }
    }
}

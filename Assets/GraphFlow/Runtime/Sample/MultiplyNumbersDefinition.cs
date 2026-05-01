using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Sample
{
    public sealed class MultiplyNumbersInstance
    {
        public int A;
        public int B;
        public int Product;
    }

    public sealed partial class MultiplyNumbersDefinition : GraphNodeDefinitionBase<MultiplyNumbersInstance>
    {
        public InputConnection<int> A;
        public InputConnection<int> B;
        public OutputConnection<int> Product;
        public FlowInput In;
        public FlowOutput Out;

        protected override ValueTask ExecuteAsync(MultiplyNumbersInstance instance, Flow flow, CancellationToken cancellationToken)
        {
            instance.Product = instance.A * instance.B;
            return default;
        }
    }
}

using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public abstract class GraphNodeDefinitionBase : IGraphNodeDefinition
    {
        public abstract string DefinitionTypeId { get; }

        public virtual object CreateInstance() => null;

        public virtual void Initialize(GraphInitializationContext context) { }

        public ValueTask ExecuteAsync(object instance, Flow flow, CancellationToken cancellationToken)
            => ExecuteAsync(flow, cancellationToken);

        public abstract ValueTask ExecuteAsync(Flow flow, CancellationToken cancellationToken);
    }

    public abstract class GraphNodeDefinitionBase<TInstance> : IGraphNodeDefinition
        where TInstance : class, new()
    {
        public abstract string DefinitionTypeId { get; }

        public object CreateInstance() => new TInstance();

        public virtual void Initialize(GraphInitializationContext context) { }

        public async ValueTask ExecuteAsync(object instance, Flow flow, CancellationToken cancellationToken)
        {
            await ExecuteAsync((TInstance)instance, flow, cancellationToken);
        }

        protected abstract ValueTask ExecuteAsync(TInstance instance, Flow flow, CancellationToken cancellationToken);
    }
}

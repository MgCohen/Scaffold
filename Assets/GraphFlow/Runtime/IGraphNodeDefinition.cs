using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public interface IGraphNodeDefinition
    {
        string DefinitionTypeId { get; }

        object CreateInstance();

        void Initialize(GraphInitializationContext context);

        ValueTask ExecuteAsync(object instance, Flow flow, CancellationToken cancellationToken);
    }
}

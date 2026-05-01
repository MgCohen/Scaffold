using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class GraphFlowRegistry : INodeExecutorRegistry
    {
        readonly Dictionary<string, IGraphNodeDefinition> map = new Dictionary<string, IGraphNodeDefinition>();
        readonly List<IGraphNodeDefinition> all = new List<IGraphNodeDefinition>();

        public IReadOnlyList<IGraphNodeDefinition> AllDefinitions => all;

        public void Register(IGraphNodeDefinition definition)
        {
            map[definition.DefinitionTypeId] = definition;
            all.Add(definition);
        }

        public IGraphNodeDefinition Resolve(string definitionTypeId)
        {
            return map.TryGetValue(definitionTypeId, out var d) ? d : null;
        }
    }
}

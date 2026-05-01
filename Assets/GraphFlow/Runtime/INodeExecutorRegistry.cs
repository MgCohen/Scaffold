namespace Scaffold.GraphFlow
{
    public interface INodeExecutorRegistry
    {
        IGraphNodeDefinition Resolve(string definitionTypeId);
    }
}

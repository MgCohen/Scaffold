namespace Scaffold.Entities
{
    public interface IEntity<out TDefinition> : IReadOnlyEntity<TDefinition> where TDefinition : IEntityDefinition
    {
        bool AddVariable(Variable key, VariableValue initialBase);

        bool RemoveVariable(Variable key);
    }
}

namespace Scaffold.Entities
{
    public interface IMutableEntity<TDefinition> : IReadOnlyEntity<TDefinition> where TDefinition : IEntityDefinition
    {
        bool AddVariable(Variable key, VariableValue initialBase);

        bool RemoveVariable(Variable key);

        ModifierId AddModifier(EntityModifierEntry entry);

        bool RemoveModifier(Variable key, ModifierId id);

        void ClearModifiers();
    }
}

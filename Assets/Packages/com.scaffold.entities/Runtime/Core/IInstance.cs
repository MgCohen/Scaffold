namespace Scaffold.Entities
{
    public interface IInstance<TDefinition> : IEntity<TDefinition> where TDefinition : IEntityDefinition
    {
        void AddModifier(EntityModifierEntry entry);

        bool RemoveModifier(EntityModifierEntry entry);

        void ClearModifiers();
    }
}

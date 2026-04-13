namespace Scaffold.Entities
{
    public interface IInstance<TDefinition> : IEntity<TDefinition> where TDefinition : EntityDefinition
    {
        void AddModifier(EntityModifierEntry entry);

        bool RemoveModifier(EntityModifierEntry entry);

        void ClearModifiers();
    }
}

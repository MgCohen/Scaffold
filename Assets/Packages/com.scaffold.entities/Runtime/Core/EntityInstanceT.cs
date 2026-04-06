namespace Scaffold.Entities
{
    /// <summary>
    /// Typed convenience subclass that exposes a strongly typed <see cref="EntityDefinition"/> without casting.
    /// </summary>
    public class EntityInstance<TDefinition> : Entity where TDefinition : EntityDefinition
    {
        public new TDefinition Definition => (TDefinition)base.Definition;
    }
}

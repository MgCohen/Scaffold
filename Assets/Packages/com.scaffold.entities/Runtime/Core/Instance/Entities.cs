namespace Scaffold.Entities
{
    public static class Entities
    {
        public static EntityInstance<TDefinition> Local<TDefinition>(TDefinition definition) where TDefinition : IEntityDefinition
            => new(definition, new LocalVariableStorage());
    }
}

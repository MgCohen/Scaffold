namespace Scaffold.Entities
{
    public readonly struct ActiveModifier
    {
        public ActiveModifier(ModifierId id, VariableModifier modifier) : this(id, modifier, null) { }

        public ActiveModifier(ModifierId id, VariableModifier modifier, ModifierSource? source)
        {
            Id = id;
            Modifier = modifier;
            Source = source;
        }

        public readonly ModifierId Id;
        public readonly VariableModifier Modifier;
        public readonly ModifierSource? Source;
    }
}

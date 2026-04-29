namespace Scaffold.Entities
{
    public readonly struct ActiveModifier
    {
        public ActiveModifier(ModifierId id, VariableModifier modifier)
        {
            Id = id;
            Modifier = modifier;
        }

        public readonly ModifierId Id;
        public readonly VariableModifier Modifier;
    }
}

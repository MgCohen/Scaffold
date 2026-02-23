namespace Scaffold.Entities
{
    public record AttributeModifier(ModifierInstanceId ModifierInstanceId, ModifierDefinitionId ModifierDefinitionId, AttributeDefinitionId AttributeDefinitionId, ModifierOperation Operation, int Value, int Priority);
}

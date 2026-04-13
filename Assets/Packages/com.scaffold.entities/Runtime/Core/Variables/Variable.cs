#nullable enable
namespace Scaffold.Entities
{
    public record Variable(string Key, VariableValueType Type = VariableValueType.String);
}

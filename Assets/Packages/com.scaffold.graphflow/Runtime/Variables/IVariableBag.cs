#nullable enable
namespace Scaffold.GraphFlow
{
    public interface IVariableBag
    {
        IVariableBag? Parent { get; }

        // Hot-path API. Callers cache the typed cell once at Initialize / bake
        // time; steady-state reads/writes go through cell.Value directly.
        bool TryGetCell<T>(string id, out VariableCell<T> cell);

        // Introspection / save-load API. Not used on hot paths.
        bool TryGetCell(string id, out VariableCell cell);
    }
}

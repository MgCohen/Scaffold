#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.GraphFlow
{
    public interface IVariableBag
    {
        IVariableBag? Parent { get; }

        // Hot-path API. Callers cache the typed cell once at Initialize / bake
        // time; steady-state reads/writes go through cell.Value directly.
        bool TryGetCell<T>(string id, [MaybeNullWhen(false)] out VariableCell<T> cell);

        // Introspection / save-load API. Not used on hot paths.
        bool TryGetCell(string id, [MaybeNullWhen(false)] out VariableCell cell);

        // Enumerates cells owned by this bag (not parents). Used by inspector,
        // save/load, and debug tooling.
        IEnumerable<VariableCell> Cells { get; }
    }
}

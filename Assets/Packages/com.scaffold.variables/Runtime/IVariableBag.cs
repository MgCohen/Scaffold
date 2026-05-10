#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.Variables
{
    public interface IVariableBag
    {
        IVariableBag? Parent { get; }

        // Hot-path API. Callers cache the typed handle once at Initialize / bake
        // time; steady-state reads/writes go through handle.Value / handle.Set
        // directly.
        bool TryGet<T>(string id, [MaybeNullWhen(false)] out IVariableHandle<T> handle);

        // Introspection / save-load API. Not used on hot paths.
        bool TryGet(string id, [MaybeNullWhen(false)] out IVariableHandle handle);

        // Enumerates handles owned by this bag (not the parent chain). Used by
        // inspector, save/load, and debug tooling.
        IEnumerable<IVariableHandle> LocalHandles { get; }
    }
}

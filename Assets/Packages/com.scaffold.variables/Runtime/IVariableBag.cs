#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.Variables
{
    public interface IVariableBag
    {
        IVariableBag? Parent { get; }

        /// <summary>
        /// Resolves a typed handle for the given variable id, walking the parent
        /// chain on miss. Hot-path API — callers cache the returned handle once
        /// (at Initialize / bake time) and read/write through it directly
        /// thereafter, so the typed lookup runs once per variable per consumer.
        /// </summary>
        /// <remarks>
        /// Type mismatch terminates the lookup: if a handle with the matching
        /// id exists locally but at an incompatible <typeparamref name="T"/>,
        /// the method returns <c>false</c> without cascading to the parent
        /// chain. A given id is treated as having a single canonical type
        /// across the entire chain.
        /// </remarks>
        bool TryGet<T>(string id, [MaybeNullWhen(false)] out IVariableHandle<T> handle);

        /// <summary>
        /// Non-generic introspection lookup, walking the parent chain on miss.
        /// Used by inspector, save/load, and debug tooling — not on hot paths.
        /// </summary>
        bool TryGet(string id, [MaybeNullWhen(false)] out IVariableHandle handle);

        /// <summary>
        /// Enumerates handles owned directly by this bag (not the parent chain).
        /// Used by inspector, save/load, and debug tooling.
        /// </summary>
        IEnumerable<IVariableHandle> LocalHandles { get; }
    }
}

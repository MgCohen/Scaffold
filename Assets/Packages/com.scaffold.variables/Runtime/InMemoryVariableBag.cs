#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.Variables
{
    public sealed class InMemoryVariableBag : IVariableBag
    {
        readonly Dictionary<string, IVariableHandle> _handles = new();

        public IVariableBag? Parent { get; }

        public InMemoryVariableBag(IVariableBag? parent = null)
        {
            Parent = parent;
        }

        // Seeds handles by calling each VariableDefault.CreateHandle(id). Skips
        // entries whose id is empty or whose default is null.
        public InMemoryVariableBag(IEnumerable<(string id, VariableDefault? @default)> seed,
                                   IVariableBag? parent = null)
            : this(parent)
        {
            if (seed == null) return;
            foreach (var (id, def) in seed)
            {
                if (string.IsNullOrEmpty(id) || def == null) continue;
                _handles[id] = def.CreateHandle(id);
            }
        }

        // Adds an explicit handle. Last-write-wins on duplicate id (matches the
        // seed constructor's behavior; useful for tests and consumer-built bags).
        public void Add(IVariableHandle handle)
        {
            if (handle == null || string.IsNullOrEmpty(handle.Id)) return;
            _handles[handle.Id] = handle;
        }

        public bool TryGet<T>(string id, [MaybeNullWhen(false)] out IVariableHandle<T> handle)
        {
            if (_handles.TryGetValue(id, out var raw))
            {
                if (raw is IVariableHandle<T> typed) { handle = typed; return true; }
                handle = null;
                return false;
            }
            return WalkParent(Parent, id, out handle, visited: null);
        }

        static bool WalkParent<T>(IVariableBag? parent, string id,
                                  [MaybeNullWhen(false)] out IVariableHandle<T> handle,
                                  HashSet<IVariableBag>? visited)
        {
            while (parent != null)
            {
                if (parent is InMemoryVariableBag p)
                {
                    visited ??= new HashSet<IVariableBag>(ReferenceEqualityComparer.Instance);
                    if (!visited.Add(p)) { handle = null; return false; }
                    if (p._handles.TryGetValue(id, out var raw))
                    {
                        if (raw is IVariableHandle<T> typed) { handle = typed; return true; }
                        handle = null;
                        return false;
                    }
                    parent = p.Parent;
                    continue;
                }
                // External IVariableBag implementation: defer to it. We can no
                // longer track visits across an opaque boundary, so cycles
                // entirely outside InMemoryVariableBag's chain are the
                // implementor's responsibility.
                return parent.TryGet<T>(id, out handle);
            }
            handle = null;
            return false;
        }

        public bool TryGet(string id, [MaybeNullWhen(false)] out IVariableHandle handle)
        {
            if (_handles.TryGetValue(id, out handle!)) return true;
            return WalkParent(Parent, id, out handle, visited: null);
        }

        static bool WalkParent(IVariableBag? parent, string id,
                               [MaybeNullWhen(false)] out IVariableHandle handle,
                               HashSet<IVariableBag>? visited)
        {
            while (parent != null)
            {
                if (parent is InMemoryVariableBag p)
                {
                    visited ??= new HashSet<IVariableBag>(ReferenceEqualityComparer.Instance);
                    if (!visited.Add(p)) { handle = null; return false; }
                    if (p._handles.TryGetValue(id, out handle!)) return true;
                    parent = p.Parent;
                    continue;
                }
                return parent.TryGet(id, out handle);
            }
            handle = null;
            return false;
        }

        public IEnumerable<IVariableHandle> LocalHandles => _handles.Values;
    }

    // Reference-equality comparer for HashSet<IVariableBag> visited tracking.
    // Avoids accidentally treating two distinct bags as equal because someone
    // override Equals on a custom IVariableBag implementation.
    sealed class ReferenceEqualityComparer : IEqualityComparer<IVariableBag>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(IVariableBag? x, IVariableBag? y) => ReferenceEquals(x, y);
        public int GetHashCode(IVariableBag obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}

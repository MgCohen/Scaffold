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
            if (Parent is InMemoryVariableBag p) return p.TryGetGuarded<T>(id, this, out handle);
            if (Parent != null) return Parent.TryGet<T>(id, out handle);
            handle = null;
            return false;
        }

        bool TryGetGuarded<T>(string id, InMemoryVariableBag origin,
                              [MaybeNullWhen(false)] out IVariableHandle<T> handle)
        {
            if (ReferenceEquals(this, origin)) { handle = null; return false; }
            if (_handles.TryGetValue(id, out var raw))
            {
                if (raw is IVariableHandle<T> typed) { handle = typed; return true; }
                handle = null;
                return false;
            }
            if (Parent is InMemoryVariableBag p) return p.TryGetGuarded<T>(id, origin, out handle);
            if (Parent != null) return Parent.TryGet<T>(id, out handle);
            handle = null;
            return false;
        }

        public bool TryGet(string id, [MaybeNullWhen(false)] out IVariableHandle handle)
        {
            if (_handles.TryGetValue(id, out handle!)) return true;
            if (Parent is InMemoryVariableBag p) return p.TryGetGuarded(id, this, out handle);
            if (Parent != null) return Parent.TryGet(id, out handle);
            handle = null;
            return false;
        }

        bool TryGetGuarded(string id, InMemoryVariableBag origin,
                           [MaybeNullWhen(false)] out IVariableHandle handle)
        {
            if (ReferenceEquals(this, origin)) { handle = null; return false; }
            if (_handles.TryGetValue(id, out handle!)) return true;
            if (Parent is InMemoryVariableBag p) return p.TryGetGuarded(id, origin, out handle);
            if (Parent != null) return Parent.TryGet(id, out handle);
            handle = null;
            return false;
        }

        public IEnumerable<IVariableHandle> LocalHandles => _handles.Values;
    }
}

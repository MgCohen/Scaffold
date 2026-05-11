#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Scaffold.Variables;

namespace Scaffold.Entities.States
{
    // Bag returned by StoreVariableBagBuilder.Build(). Stores the resolved
    // handle map and the cleanup callbacks the builder accumulated while
    // installing per-slice subscriptions. Dispose runs every cleanup once;
    // subsequent calls are no-ops.
    public sealed class StoreBackedVariableBag : IVariableBag, IDisposable
    {
        readonly Dictionary<string, IVariableHandle> _handles;
        readonly List<Action> _cleanups;
        bool _disposed;

        public IVariableBag? Parent => null;

        internal StoreBackedVariableBag(Dictionary<string, IVariableHandle> handles, List<Action> cleanups)
        {
            _handles = handles;
            _cleanups = cleanups;
        }

        public bool TryGet<T>(string id, [MaybeNullWhen(false)] out IVariableHandle<T> handle)
        {
            if (_handles.TryGetValue(id, out var raw) && raw is IVariableHandle<T> typed)
            {
                handle = typed;
                return true;
            }
            handle = null;
            return false;
        }

        public bool TryGet(string id, [MaybeNullWhen(false)] out IVariableHandle handle)
        {
            if (_handles.TryGetValue(id, out handle!)) return true;
            handle = null;
            return false;
        }

        // Read-only typed accessor. Read-only bindings (BindReadOnly, BindComputed)
        // produce IReadOnlyVariableHandle<T> handles that do not implement the
        // writable IVariableHandle<T>, so the shared IVariableBag.TryGet<T> path
        // would miss them. This accessor returns either kind of handle.
        public bool TryGetReadOnly<T>(string id, [MaybeNullWhen(false)] out IReadOnlyVariableHandle<T> handle)
        {
            if (_handles.TryGetValue(id, out var raw) && raw is IReadOnlyVariableHandle<T> typed)
            {
                handle = typed;
                return true;
            }
            handle = null;
            return false;
        }

        public IEnumerable<IVariableHandle> LocalHandles => _handles.Values;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _cleanups.Count; i++)
            {
                _cleanups[i].Invoke();
            }
            _cleanups.Clear();
            _handles.Clear();
        }
    }
}

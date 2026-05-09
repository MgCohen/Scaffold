#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.GraphFlow
{
    public sealed class InMemoryVariableBag : IVariableBag
    {
        readonly Dictionary<string, VariableCell> _cells = new();

        public IVariableBag? Parent { get; }

        public InMemoryVariableBag(IEnumerable<RuntimeVariable> seed, IVariableBag? parent = null)
        {
            Parent = parent;
            foreach (var v in seed)
            {
                if (v == null || string.IsNullOrEmpty(v.id) || v.defaultValue == null) continue;
                _cells[v.id] = v.defaultValue.CreateCell(v.id);
            }
        }

        public bool TryGetCell<T>(string id, [MaybeNullWhen(false)] out VariableCell<T> cell)
        {
            if (_cells.TryGetValue(id, out var raw))
            {
                if (raw is VariableCell<T> typed) { cell = typed; return true; }
                cell = null;
                return false;
            }
            if (Parent is InMemoryVariableBag p) return p.TryGetCellGuarded<T>(id, this, out cell);
            if (Parent != null) return Parent.TryGetCell<T>(id, out cell);
            cell = null;
            return false;
        }

        bool TryGetCellGuarded<T>(string id, InMemoryVariableBag origin, [MaybeNullWhen(false)] out VariableCell<T> cell)
        {
            if (ReferenceEquals(this, origin)) { cell = null; return false; }
            if (_cells.TryGetValue(id, out var raw))
            {
                if (raw is VariableCell<T> typed) { cell = typed; return true; }
                cell = null;
                return false;
            }
            if (Parent is InMemoryVariableBag p) return p.TryGetCellGuarded<T>(id, origin, out cell);
            if (Parent != null) return Parent.TryGetCell<T>(id, out cell);
            cell = null;
            return false;
        }

        public bool TryGetCell(string id, [MaybeNullWhen(false)] out VariableCell cell)
        {
            if (_cells.TryGetValue(id, out var raw)) { cell = raw; return true; }
            if (Parent is InMemoryVariableBag p) return p.TryGetCellGuarded(id, this, out cell);
            if (Parent != null) return Parent.TryGetCell(id, out cell);
            cell = null;
            return false;
        }

        bool TryGetCellGuarded(string id, InMemoryVariableBag origin, [MaybeNullWhen(false)] out VariableCell cell)
        {
            if (ReferenceEquals(this, origin)) { cell = null; return false; }
            if (_cells.TryGetValue(id, out var raw)) { cell = raw; return true; }
            if (Parent is InMemoryVariableBag p) return p.TryGetCellGuarded(id, origin, out cell);
            if (Parent != null) return Parent.TryGetCell(id, out cell);
            cell = null;
            return false;
        }

        public IEnumerable<VariableCell> Cells => _cells.Values;
    }
}

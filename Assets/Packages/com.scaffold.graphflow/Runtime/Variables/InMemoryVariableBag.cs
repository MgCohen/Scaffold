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
            // Type-mismatch shadows the parent: a child scope that declares an id at the
            // wrong type fully shadows whatever the parent has under that id, by design.
            // Cascading on type mismatch would silently bypass the child's intent.
            if (_cells.TryGetValue(id, out var raw))
            {
                if (raw is VariableCell<T> typed) { cell = typed; return true; }
                cell = null;
                return false;
            }
            if (Parent != null) return Parent.TryGetCell<T>(id, out cell);
            cell = null;
            return false;
        }

        public bool TryGetCell(string id, [MaybeNullWhen(false)] out VariableCell cell)
        {
            if (_cells.TryGetValue(id, out var raw)) { cell = raw; return true; }
            if (Parent != null) return Parent.TryGetCell(id, out cell);
            cell = null;
            return false;
        }
    }
}

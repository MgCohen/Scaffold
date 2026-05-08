#nullable enable
using System.Collections.Generic;

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
                if (string.IsNullOrEmpty(v.id) || v.defaultValue == null) continue;
                _cells[v.id] = v.defaultValue.CreateCell(v.id);
            }
        }

        public bool TryGetCell<T>(string id, out VariableCell<T> cell)
        {
            if (_cells.TryGetValue(id, out var raw))
            {
                if (raw is VariableCell<T> typed) { cell = typed; return true; }
                cell = null!;
                return false;
            }
            if (Parent != null) return Parent.TryGetCell<T>(id, out cell);
            cell = null!;
            return false;
        }

        public bool TryGetCell(string id, out VariableCell cell)
        {
            if (_cells.TryGetValue(id, out cell!)) return true;
            if (Parent != null) return Parent.TryGetCell(id, out cell);
            cell = null!;
            return false;
        }
    }
}

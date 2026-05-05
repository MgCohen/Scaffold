#nullable enable
using System.Collections.Generic;
using System.Threading;

namespace Scaffold.GraphFlow
{
    public enum Outcome { Running, Returned, Cancelled }

    public sealed class Flow
    {
        readonly object _payload;
        Dictionary<object, object>? _slots;
        readonly List<Port> _touched = new();
        object? _result;

        public GraphRunner Runner { get; }
        public CancellationToken Token { get; }

        public Outcome Outcome { get; private set; } = Outcome.Running;
        public bool IsCancelled => Outcome == Outcome.Cancelled;
        public bool IsTerminating => Outcome != Outcome.Running;

        internal Flow(object payload, GraphRunner runner, CancellationToken token)
        {
            _payload = payload;
            Runner = runner;
            Token = token;
        }

        public T? GetPayload<T>() where T : class => _payload as T;

        public FlowOutPort Return<T>(T value)
        {
            _result = value;
            Outcome = Outcome.Returned;
            return FlowOutPort.End;
        }

        public FlowOutPort Return()
        {
            _result = null;
            Outcome = Outcome.Returned;
            return FlowOutPort.End;
        }

        public FlowOutPort Cancel()
        {
            Outcome = Outcome.Cancelled;
            return FlowOutPort.End;
        }

        public T? ReadResult<T>() => _result is T t ? t : default;

        internal void RegisterTouched(Port p) => _touched.Add(p);

        public void InvalidateAll()
        {
            foreach (var p in _touched) p.ClearCache(this);
            _touched.Clear();
        }

        public T GetSlot<T>(object owner) =>
            _slots != null && _slots.TryGetValue(owner, out var v) ? (T)v : default!;

        public void SetSlot<T>(object owner, T value) =>
            (_slots ??= new())[owner] = value!;
    }
}

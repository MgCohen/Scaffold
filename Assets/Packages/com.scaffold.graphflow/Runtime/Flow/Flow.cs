#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public enum Outcome { Running, Returned, Cancelled }

    public sealed class Flow
    {
        readonly object _payload;
        Dictionary<object, object>? _slots;
        Dictionary<Port, object?>? _cache;
        object? _result;
        IVariableBag? _variables;

        public GraphRunner Runner { get; }
        public CancellationToken Token { get; }

        public IVariableBag Variables =>
            _variables ??= new InMemoryVariableBag(Runner.Variables);

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

        public FlowOutPort? Return<T>(T value)
        {
            _result = value;
            Outcome = Outcome.Returned;
            return null;
        }

        public FlowOutPort? Return()
        {
            _result = null;
            Outcome = Outcome.Returned;
            return null;
        }

        public FlowOutPort? Cancel()
        {
            Outcome = Outcome.Cancelled;
            return null;
        }

        public T? ReadResult<T>() => _result is T t ? t : default;

        internal T ReadCached<T>(Port port, Func<Flow, T> compute)
        {
            _cache ??= new();
            if (_cache.TryGetValue(port, out var v)) return (T)v!;
            var fresh = compute(this);
            _cache[port] = fresh;
            return fresh;
        }

        public void InvalidateAll() => _cache?.Clear();

        public T GetSlot<T>(object owner) =>
            _slots != null && _slots.TryGetValue(owner, out var v) ? (T)v : default!;

        public void SetSlot<T>(object owner, T value) =>
            (_slots ??= new())[owner] = value!;
    }
}

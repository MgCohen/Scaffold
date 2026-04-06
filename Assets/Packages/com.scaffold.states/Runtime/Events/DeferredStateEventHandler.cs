#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    /// <summary>
    /// Decorates an <see cref="IStateEventHandler"/>: forwards subscriptions to the inner handler and optionally buffers <see cref="Notify"/> while deferral depth is greater than zero.
    /// Single-threaded (main thread); not safe for concurrent use.
    /// </summary>
    public sealed class DeferredStateEventHandler : IStateEventHandler, IStateEventDeferralController
    {
        public DeferredStateEventHandler(IStateEventHandler inner, StateEventMergeMode mergeMode)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.mergeMode = mergeMode;
        }

        private readonly IStateEventHandler inner;
        private readonly StateEventMergeMode mergeMode;
        private int deferralDepth;
        private readonly List<(IReference Reference, BaseState State)> preserveAll = new();
        private readonly List<(IReference Reference, Type StateType)> latestKeyOrder = new();
        private readonly Dictionary<(IReference Reference, Type StateType), BaseState> latestByKey = new();

        public void Notify(IReference reference, BaseState state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var r = reference ?? Reference.Null;

            if (deferralDepth == 0)
            {
                inner.Notify(r, state);
                return;
            }

            BufferDeferred(r, state);
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState> action) where TState : BaseState
        {
            inner.Subscribe(reference, action);
        }

        public void SubscribeAllReferences<TState>(Action<IReference, TState> action) where TState : BaseState
        {
            inner.SubscribeAllReferences(action);
        }

        public void SubscribeAny(Action<IReference, BaseState> action)
        {
            inner.SubscribeAny(action);
        }

        public void Flush()
        {
            if (mergeMode == StateEventMergeMode.PreserveAll)
            {
                FlushPreserveAll();
            }
            else
            {
                FlushLatestPerKey();
            }
        }

        public IDisposable BeginDeferScope()
        {
            return new DeferScope(this);
        }

        private void BufferDeferred(IReference r, BaseState state)
        {
            if (mergeMode == StateEventMergeMode.PreserveAll)
            {
                preserveAll.Add((r, state));
                return;
            }

            var key = (r, state.GetType());
            if (!latestByKey.ContainsKey(key))
            {
                latestKeyOrder.Add(key);
            }

            latestByKey[key] = state;
        }

        private void FlushPreserveAll()
        {
            while (preserveAll.Count > 0)
            {
                var snapshot = preserveAll.ToArray();
                preserveAll.Clear();
                foreach (var (reference, st) in snapshot)
                {
                    inner.Notify(reference, st);
                }
            }
        }

        private void FlushLatestPerKey()
        {
            while (latestKeyOrder.Count > 0)
            {
                var snapshot = latestKeyOrder.ToArray();
                latestKeyOrder.Clear();
                foreach (var key in snapshot)
                {
                    if (latestByKey.TryGetValue(key, out BaseState? st))
                    {
                        latestByKey.Remove(key);
                        inner.Notify(key.Reference, st);
                    }
                }
            }
        }

        private void EnterDeferScope()
        {
            deferralDepth++;
        }

        private void LeaveDeferScope()
        {
            if (deferralDepth == 0)
            {
                throw new InvalidOperationException("Defer scope is unbalanced (extra dispose).");
            }

            deferralDepth--;
        }

        private sealed class DeferScope : IDisposable
        {
            public DeferScope(DeferredStateEventHandler owner)
            {
                this.owner = owner;
                owner.EnterDeferScope();
            }

            private readonly DeferredStateEventHandler owner;
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                owner.LeaveDeferScope();
            }
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
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
        private readonly List<(IReference Reference, BaseState State, StateChangeEvent Change)> preserveAll = new();
        private readonly List<(IReference Reference, Type StateType)> latestKeyOrder = new();
        private readonly Dictionary<(IReference Reference, Type StateType), (BaseState State, StateChangeEvent Change)> latestByKey = new();

        public void Notify(IReference reference, BaseState state)
        {
            Notify(reference, state, StateChangeEvent.Updated);
        }

        public void Notify(IReference reference, BaseState state, StateChangeEvent changeEvent)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var r = reference ?? Reference.Null;

            if (deferralDepth == 0)
            {
                inner.Notify(r, state, changeEvent);
                return;
            }

            BufferDeferred(r, state, changeEvent);
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.Subscribe(reference, action);
        }

        public void Unsubscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.Unsubscribe(reference, action);
        }

        public void SubscribeAllReferences<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.SubscribeAllReferences(action);
        }

        public void SubscribeAny(Action<IReference, BaseState, StateChangeEvent> action)
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

        private void BufferDeferred(IReference r, BaseState state, StateChangeEvent changeEvent)
        {
            if (mergeMode == StateEventMergeMode.PreserveAll)
            {
                preserveAll.Add((r, state, changeEvent));
                return;
            }

            var key = (r, state.GetType());
            if (!latestByKey.ContainsKey(key))
            {
                latestKeyOrder.Add(key);
            }

            latestByKey[key] = (state, changeEvent);
        }

        private void FlushPreserveAll()
        {
            while (preserveAll.Count > 0)
            {
                var snapshot = preserveAll.ToArray();
                preserveAll.Clear();
                foreach (var (reference, st, ev) in snapshot)
                {
                    inner.Notify(reference, st, ev);
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
                    if (latestByKey.TryGetValue(key, out var entry))
                    {
                        latestByKey.Remove(key);
                        inner.Notify(key.Reference, entry.State, entry.Change);
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

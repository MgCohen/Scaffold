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
        private List<(Reference Reference, BaseState State, StateChangeEvent Change)> preserveDeferred = new();
        private List<(Reference Reference, BaseState State, StateChangeEvent Change)> preserveScratch = new();
        private List<(Reference Reference, Type StateType)> latestOrder = new();
        private List<(Reference Reference, Type StateType)> latestScratch = new();
        private readonly Dictionary<(Reference Reference, Type StateType), (BaseState State, StateChangeEvent Change)> latestByKey = new();

        public void Notify(Reference reference, BaseState state, StateChangeEvent changeEvent)
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

        public void Subscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.Subscribe(reference, action);
        }

        public void Subscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.Subscribe(reference, action);
        }

        public void Subscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState
        {
            inner.Subscribe(reference, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.Unsubscribe(reference, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.Unsubscribe(reference, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState
        {
            inner.Unsubscribe(reference, action);
        }

        public void SubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.SubscribeAllReferences(action);
        }

        public void UnsubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            inner.UnsubscribeAllReferences(action);
        }

        public void SubscribeAny(Action<Reference, BaseState, StateChangeEvent> action)
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

        private void BufferDeferred(Reference r, BaseState state, StateChangeEvent changeEvent)
        {
            if (mergeMode == StateEventMergeMode.PreserveAll)
            {
                preserveDeferred.Add((r, state, changeEvent));
                return;
            }

            var key = (r, state.GetType());
            if (latestByKey.TryAdd(key, (state, changeEvent)))
            {
                latestOrder.Add(key);
            }
            else
            {
                latestByKey[key] = (state, changeEvent);
            }
        }

        private void FlushPreserveAll()
        {
            while (preserveDeferred.Count > 0)
            {
                var swap = preserveDeferred;
                preserveDeferred = preserveScratch;
                preserveScratch = swap;

                foreach (var (reference, st, ev) in preserveScratch)
                {
                    inner.Notify(reference, st, ev);
                }

                preserveScratch.Clear();
            }
        }

        private void FlushLatestPerKey()
        {
            while (latestOrder.Count > 0)
            {
                var swap = latestOrder;
                latestOrder = latestScratch;
                latestScratch = swap;

                foreach (var key in latestScratch)
                {
                    if (latestByKey.TryGetValue(key, out var entry))
                    {
                        latestByKey.Remove(key);
                        inner.Notify(key.Reference, entry.State, entry.Change);
                    }
                }

                latestScratch.Clear();
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

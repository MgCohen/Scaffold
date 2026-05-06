#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    public readonly struct EnumerateAllPairsResult<TState> where TState : BaseState
    {
        internal EnumerateAllPairsResult(Store store)
        {
            this.store = store;
        }

        private readonly Store store;

        public Enumerator GetEnumerator()
        {
            return new Enumerator(store);
        }

        public struct Enumerator : IDisposable
        {
            internal Enumerator(Store owner)
            {
                this.owner = owner;
                buffer = owner.RentFilledEnumerationBuffer(typeof(TState));
                index = -1;
                current = default;
            }

            public (Reference Reference, TState State) Current => current;

            private readonly Store owner;
            private List<BaseSlice>? buffer;
            private int index;
            private (Reference Reference, TState State) current;

            public bool MoveNext()
            {
                while (buffer != null && ++index < buffer.Count)
                {
                    BaseSlice slot = buffer[index];
                    if (slot.State is TState ts)
                    {
                        current = (slot.Reference, ts);
                        return true;
                    }
                }

                return false;
            }

            public void Dispose()
            {
                if (buffer is null)
                {
                    return;
                }

                owner.ReturnEnumerationBuffer(buffer);
                buffer = null;
            }
        }
    }
}

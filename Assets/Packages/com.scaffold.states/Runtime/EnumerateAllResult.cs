#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    public readonly struct EnumerateAllResult<TState> where TState : BaseState
    {
        internal EnumerateAllResult(Store store)
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
                current = default!;
            }

            public TState Current => current;

            private readonly Store owner;
            private List<BaseSlice>? buffer;
            private int index;
            private TState current;

            public bool MoveNext()
            {
                while (buffer != null && ++index < buffer.Count)
                {
                    if (buffer[index].State is TState ts)
                    {
                        current = ts;
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

using System;
using System.Collections;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    /// <summary>
    /// Read-only projection over an indexer’s tracked entries. Enumeration walks the keyed set without allocating a snapshot list per read.
    /// </summary>
    public readonly struct IndexerValuesView<TPrimary, TSecondary, TValue> : IReadOnlyCollection<TValue>
    {
        private readonly Indexer<TPrimary, TSecondary, TValue> _indexer;

        internal IndexerValuesView(Indexer<TPrimary, TSecondary, TValue> indexer)
        {
            _indexer = indexer;
        }

        /// <inheritdoc cref="Indexer{TPrimary,TSecondary,TValue}.TrackedCount"/>
        public int Count => _indexer is null ? 0 : _indexer.TrackedCount;

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_indexer);
        }

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<TValue>
        {
            private readonly Indexer<TPrimary, TSecondary, TValue> _indexer;
            private HashSet<Index<TPrimary, TSecondary>>.Enumerator _setEnum;
            private TValue _current;

            internal Enumerator(Indexer<TPrimary, TSecondary, TValue> indexer)
            {
                _indexer = indexer;
                _setEnum = indexer?.GetTrackedKeyEnumerator() ?? default;
                _current = default;
            }

            public TValue Current => _current;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (_indexer is null)
                {
                    return false;
                }

                while (_setEnum.MoveNext())
                {
                    if (_indexer.OwnerTryGetValue(_setEnum.Current, out TValue value))
                    {
                        _current = value;
                        return true;
                    }
                }

                return false;
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose()
            {
            }
        }
    }
}

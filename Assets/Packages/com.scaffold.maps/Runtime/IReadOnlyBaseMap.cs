#pragma warning disable SCA3002 // Second interface kept in this file: Unity csproj explicit Compile list often omits new IReadOnlyMap.cs after regen (CS0246 for dotnet/IDE).

using System.Collections.Generic;

namespace Scaffold.Maps
{
    public interface IReadOnlyBaseMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        int Count { get; }

        TValue this[TKey key] { get; }

        IEnumerable<TValue> Values { get; }

        bool ContainsKey(TKey key);

        bool TryGetValue(TKey key, out TValue value);
    }

    public interface IReadOnlyMap<TPrimary, TSecondary, TValue> : IReadOnlyBaseMap<Index<TPrimary, TSecondary>, TValue>
    {
        TValue this[TPrimary primary, TSecondary secondary] { get; }

        bool Contains(TPrimary primary, TSecondary secondary);

        bool TryGetValue(TPrimary primary, TSecondary secondary, out TValue value);

        IReadOnlyCollection<TValue> GetIndexedValues(string name);

        bool TryGetIndexer(string name, out Indexer<TPrimary, TSecondary, TValue> indexer);

        IReadOnlyList<KeyValuePair<TSecondary, TValue>> GetAll(TPrimary primary);

        IReadOnlyList<KeyValuePair<TPrimary, TValue>> GetAll(TSecondary secondary);

        IReadOnlyCollection<TPrimary> GetPrimaryKeys();

        IReadOnlyCollection<TSecondary> GetSecondaryKeys();
    }
}

#pragma warning restore SCA3002

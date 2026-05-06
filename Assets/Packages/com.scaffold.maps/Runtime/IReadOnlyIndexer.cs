using System.Collections.Generic;

namespace Scaffold.Maps
{
    /// <summary>
    /// Read-only indexer view. Predicates filter by composite key only; value mutations do not reclassify entries.
    /// </summary>
    public interface IReadOnlyIndexer<TPrimary, TSecondary, TValue>
    {
        string Name { get; }

        int Count { get; }

        /// <summary>
        /// Values tracked by composite key — use <see cref="IndexerValuesView{TPrimary,TSecondary,TValue}.Count"/> for O(1) cardinality without enumerating.
        /// </summary>
        IndexerValuesView<TPrimary, TSecondary, TValue> Values { get; }
    }
}

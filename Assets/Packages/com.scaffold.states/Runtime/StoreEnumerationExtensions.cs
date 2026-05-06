#nullable enable
using System.Collections.Generic;

namespace Scaffold.States
{
    public static class StoreEnumerationExtensions
    {
        public static IEnumerable<(Reference Reference, TState State)> EnumerateAll<TState>(this Store store) where TState : BaseState
        {
            foreach (var pair in store.EnumerateAllPairs<TState>())
            {
                yield return pair;
            }
        }
    }
}

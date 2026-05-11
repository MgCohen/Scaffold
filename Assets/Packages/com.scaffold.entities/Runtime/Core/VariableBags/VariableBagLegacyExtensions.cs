#nullable enable
using System;
using Scaffold.Variables;

namespace Scaffold.Entities
{
    [Obsolete("Use IVariableBag.TryGet<T> instead. Will be removed in a future release.")]
    public static class VariableBagLegacyExtensions
    {
        public static bool TryGetBase(this IVariableBag bag, Variable key, out VariableValue value)
        {
            if (bag is VariableBag vb)
            {
#pragma warning disable CS0612, CS0618
                return vb.TryGetBase(key, out value);
#pragma warning restore CS0612, CS0618
            }
            if (bag is IEntityVariableStorage storage)
            {
                return storage.TryGetBase(key, out value);
            }
            value = default!;
            return false;
        }
    }
}

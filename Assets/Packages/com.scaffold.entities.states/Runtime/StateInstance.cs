#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public class StateInstance<TDefinition> : EntityInstance<TDefinition>, ISliceProvider
        where TDefinition : IEntityDefinition
    {
        public StateInstance(TDefinition definition, IEntityVariableStorage storage)
            : base(definition, storage) { }

        public virtual IEnumerable<State> ProvideInitialSlices()
        {
            if (Definition is ISliceProvider provider)
            {
                return provider.ProvideInitialSlices();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning(
                $"{typeof(TDefinition).Name} does not implement ISliceProvider; " +
                $"{GetType().Name} will return no initial slices.");
#endif
            return Array.Empty<State>();
        }
    }
}

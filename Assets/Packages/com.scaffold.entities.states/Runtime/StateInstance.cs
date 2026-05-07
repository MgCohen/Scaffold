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
            => Definition is ISliceProvider provider
                ? provider.ProvideInitialSlices()
                : Array.Empty<State>();
    }
}

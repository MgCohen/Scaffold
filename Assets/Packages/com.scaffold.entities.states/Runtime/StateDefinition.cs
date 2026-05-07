#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Serializable]
    public class StateDefinition : EntityDefinition, ISliceProvider
    {
        public virtual IEnumerable<State> ProvideInitialSlices() => Array.Empty<State>();
    }
}

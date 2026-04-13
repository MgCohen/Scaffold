using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public abstract class VariableValue
    {
        public abstract VariableValueType Type { get; }

        public abstract VariableValue Combine(IReadOnlyList<VariableValue> contributions);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace Scaffold.Entities
{
    [Preserve]
    [Serializable]
    public abstract class VariableValue
    {
        public abstract VariableValue ApplyModifiers(IReadOnlyList<ActiveModifier> modifiers);
    }
}

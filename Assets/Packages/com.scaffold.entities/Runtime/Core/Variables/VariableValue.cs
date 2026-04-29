using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace Scaffold.Entities
{
    [Preserve]
    [Serializable]
    public abstract class VariableValue
    {
        internal abstract VariableValue ApplyModifiers(IReadOnlyList<ActiveModifier> modifiers);
    }
}

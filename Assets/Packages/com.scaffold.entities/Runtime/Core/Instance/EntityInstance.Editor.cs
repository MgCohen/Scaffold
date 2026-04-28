#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    public partial class EntityInstance<TDefinition> where TDefinition : IEntityDefinition
    {
        internal void NotifyAllEffectiveValues()
        {
            localStorage?.NotifyAllEffectiveValues();
        }

        internal void EditorApplyVariableAuthoringOnBagsFromValidation()
        {
            localStorage?.EditorApplyVariableAuthoringOnBagsFromValidation();
        }
    }
}
#endif

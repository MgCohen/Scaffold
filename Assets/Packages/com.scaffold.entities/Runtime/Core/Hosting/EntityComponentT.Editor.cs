#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Scaffold.Entities
{
    public partial class EntityComponent<TDefinition> : EntityComponent, IMutableEntity<TDefinition> where TDefinition : IEntityDefinition
    {
        private void OnValidate()
        {
            instance?.EditorApplyVariableAuthoringOnBagsFromValidation();

            if (!Application.isPlaying)
            {
                return;
            }

            instance?.NotifyAllEffectiveValues();
        }
    }
}
#endif

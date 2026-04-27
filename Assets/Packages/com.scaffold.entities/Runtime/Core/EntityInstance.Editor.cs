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
            if (notifier == null || instanceEffectiveBag == null)
            {
                return;
            }

            foreach (Variable key in instanceEffectiveBag.LocalKeys)
            {
                if (instanceEffectiveBag.TryGetBase(key, out VariableValue value))
                {
                    notifier.Notify(key, value);
                }
            }
        }

        internal void EditorApplyVariableAuthoringOnBagsFromValidation()
        {
            if (instanceBaseBag == null)
            {
                instanceBaseBag = new VariableBag();
            }

            if (instanceEffectiveBag == null)
            {
                instanceEffectiveBag = new VariableBag();
            }

            instanceBaseBag.EditorApplyVariableAuthoringFromValidation();
            instanceEffectiveBag.EditorApplyVariableAuthoringFromValidation();
        }
    }
}
#endif

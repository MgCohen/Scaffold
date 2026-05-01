#if UNITY_EDITOR
namespace Scaffold.Entities
{
    public sealed partial class LocalVariableStorage
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
            instanceBaseBag.EditorApplyVariableAuthoringFromValidation();
            instanceEffectiveBag.EditorApplyVariableAuthoringFromValidation();
        }
    }
}
#endif

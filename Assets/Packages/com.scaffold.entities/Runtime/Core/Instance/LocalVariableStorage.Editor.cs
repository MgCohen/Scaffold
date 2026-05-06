#if UNITY_EDITOR
namespace Scaffold.Entities
{
    public sealed partial class LocalVariableStorage
    {
        internal void EditorApplyVariableAuthoringOnBagsFromValidation()
        {
            instanceBaseBag.EditorApplyVariableAuthoringFromValidation();
        }
    }
}
#endif

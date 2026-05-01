#if UNITY_EDITOR
namespace Scaffold.Entities
{
    public partial class EntityDefinitionAsset
    {
        partial void EditorAfterValidateBeforeRebuild()
        {
            definition.Bag.EditorApplyVariableAuthoringFromValidation();
        }
    }
}
#endif

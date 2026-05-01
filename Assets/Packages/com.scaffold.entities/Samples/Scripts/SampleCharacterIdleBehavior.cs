using UnityEngine;

namespace Scaffold.Entities.Samples
{
    public sealed class SampleCharacterIdleBehavior : MonoBehaviour, IEntityBehavior<SampleCharacterEntity, SampleCharacterInput>
    {
        public bool TryAcceptControl(SampleCharacterEntity data, in SampleCharacterInput input)
        {
            return true;
        }

        public void Execute(SampleCharacterEntity data, in SampleCharacterInput input, float deltaTime)
        {
        }

        public void OnQuit(SampleCharacterEntity data)
        {
        }
    }
}

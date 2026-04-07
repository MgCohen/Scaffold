using UnityEngine;

namespace Scaffold.Entities.Samples
{
    /// <summary>
    /// Fallback behavior when the character is not moving (ordered after <see cref="SampleCharacterMoveBehavior"/>).
    /// </summary>
    public sealed class SampleCharacterIdleBehavior : MonoBehaviour, IEntityBehavior<Entity, SampleCharacterInput>
    {
        public bool TryAcceptControl(Entity data, in SampleCharacterInput input)
        {
            return true;
        }

        public void Execute(Entity data, in SampleCharacterInput input, float deltaTime)
        {
        }

        public void OnQuit(Entity data)
        {
        }
    }
}

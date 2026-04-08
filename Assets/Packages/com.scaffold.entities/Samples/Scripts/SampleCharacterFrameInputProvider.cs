using UnityEngine;

namespace Scaffold.Entities.Samples
{
    /// <summary>
    /// Maps keyboard axes to <see cref="SampleCharacterInput"/> for <see cref="SampleCharacterBehaviorRunner"/>.
    /// </summary>
    public sealed class SampleCharacterFrameInputProvider : MonoBehaviour,
        IEntityFrameInputProvider<SampleCharacterInput>
    {
        public SampleCharacterInput GetFrameInput()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            var move = new Vector2(x, z);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            return new SampleCharacterInput(move);
        }
    }
}

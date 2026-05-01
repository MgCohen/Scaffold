using UnityEngine;

namespace Scaffold.Entities.Samples
{
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

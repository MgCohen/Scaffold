using System.Globalization;
using UnityEngine;

namespace Scaffold.Entities.Samples
{
    /// <summary>
    /// Moves the entity on the XZ plane using the effective Move Speed attribute (numeric payload).
    /// </summary>
    public sealed class SampleCharacterMoveBehavior : MonoBehaviour, IEntityBehavior<Entity, SampleCharacterInput>
    {
        [SerializeField]
        private float deadzone = 0.01f;

        [SerializeField]
        private AttributeSO moveSpeedAttribute = default!;

        public bool TryAcceptControl(Entity data, in SampleCharacterInput input)
        {
            return input.Move.sqrMagnitude > deadzone * deadzone;
        }

        public void Execute(Entity data, in SampleCharacterInput input, float deltaTime)
        {
            if (moveSpeedAttribute == null || !data.TryGetAttribute(moveSpeedAttribute, out Attribute speedAttr))
            {
                return;
            }

            if (!float.TryParse(
                    speedAttr.Payload,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float speed))
            {
                return;
            }

            Vector3 delta = new Vector3(input.Move.x, 0f, input.Move.y) * (speed * deltaTime);
            data.transform.position += delta;
        }

        public void OnQuit(Entity data)
        {
        }
    }
}

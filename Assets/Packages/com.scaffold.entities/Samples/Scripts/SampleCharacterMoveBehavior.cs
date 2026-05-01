using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities.Samples
{
    public sealed class SampleCharacterMoveBehavior : MonoBehaviour, IEntityBehavior<SampleCharacterEntity, SampleCharacterInput>
    {
        [SerializeField]
        private float deadzone = 0.01f;

        [FormerlySerializedAs("moveSpeedAttribute")]
        [SerializeField]
        private VariableSO moveSpeedVariable = default!;

        public bool TryAcceptControl(SampleCharacterEntity data, in SampleCharacterInput input)
        {
            return input.Move.sqrMagnitude > deadzone * deadzone;
        }

        public void Execute(SampleCharacterEntity data, in SampleCharacterInput input, float deltaTime)
        {
            if (moveSpeedVariable == null ||
                !data.TryGetVariable(moveSpeedVariable, out FloatVariableValue floatSpeed))
            {
                return;
            }

            float speed = floatSpeed.Value;
            Vector3 delta = new Vector3(input.Move.x, 0f, input.Move.y) * (speed * deltaTime);
            data.transform.position += delta;
        }

        public void OnQuit(SampleCharacterEntity data)
        {
        }
    }
}

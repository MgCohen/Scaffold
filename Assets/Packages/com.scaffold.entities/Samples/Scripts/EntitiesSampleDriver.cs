using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities.Samples
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SampleCharacterEntity))]
    public sealed class EntitiesSampleDriver : MonoBehaviour
    {
        [SerializeField]
        private SampleCharacterDefinition characterDefinition = default!;

        [FormerlySerializedAs("healthAttribute")]
        [SerializeField]
        private VariableSO healthVariable = default!;

        [FormerlySerializedAs("moveSpeedAttribute")]
        [SerializeField]
        private VariableSO moveSpeedVariable = default!;

        [SerializeField]
        private EntityModifierEntryAsset healthModifier = default!;

        [SerializeField]
        private bool showDebugHud = true;

        private SampleCharacterEntity entity = default!;

        private readonly EntityInstanceCreator<SampleCharacterDefinition> instanceCreator =
            new EntityInstanceCreator<SampleCharacterDefinition>(new IncrementingInstanceIdGenerator());

        private void Awake()
        {
            entity = GetComponent<SampleCharacterEntity>();
            if (characterDefinition != null)
            {
                instanceCreator.InitializeComponent(entity, characterDefinition);
            }
        }

        private void Start()
        {
            LogEffectiveStats("Initial");
            if (healthModifier != null)
            {
                entity.AddModifier((EntityModifierEntry)healthModifier);
            }

            LogEffectiveStats("After +25 health modifier (numeric slots are summed)");
        }

        private void LogEffectiveStats(string label)
        {
            if (healthVariable != null &&
                entity.TryGetVariable(healthVariable, out VariableValue health))
            {
                Debug.Log($"[Entities Sample] {label} — Health effective: {FormatVariableText(health)}", this);
            }

            if (moveSpeedVariable != null &&
                entity.TryGetVariable(moveSpeedVariable, out VariableValue speed))
            {
                Debug.Log($"[Entities Sample] {label} — Move Speed effective: {FormatVariableText(speed)}", this);
            }
        }

        private void OnGUI()
        {
            if (showDebugHud)
            {
                DrawDebugHud();
            }
        }

        private void DrawDebugHud()
        {
            GUILayout.BeginArea(new Rect(8, 8, 420, 140), GUI.skin.box);
            GUILayout.Label("Scaffold.Entities sample — WASD / arrows to move (XZ)");
            GUILayout.Label($"Instance: {entity.Id}");
            DrawVariableLine("Health (effective)", healthVariable);
            DrawVariableLine("Move speed (effective)", moveSpeedVariable);
            GUILayout.EndArea();
        }

        private void DrawVariableLine(string label, VariableSO slot)
        {
            if (slot != null && entity.TryGetVariable(slot, out VariableValue value))
            {
                GUILayout.Label($"{label}: {FormatVariableText(value)}");
            }
        }

        private string FormatVariableText(VariableValue value)
        {
            return value switch
            {
                FloatVariableValue f => f.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                IntVariableValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BoolVariableValue b => b.Value.ToString(),
                StringVariableValue s => s.Value,
                _ => value?.ToString() ?? "(null)"
            };
        }
    }
}

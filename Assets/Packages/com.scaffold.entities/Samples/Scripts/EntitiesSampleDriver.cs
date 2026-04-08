using UnityEngine;

namespace Scaffold.Entities.Samples
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SampleCharacterEntity))]
    public sealed class EntitiesSampleDriver : MonoBehaviour
    {
        [SerializeField]
        private SampleCharacterDefinition characterDefinition = default!;

        [SerializeField]
        private AttributeSO healthAttribute = default!;

        [SerializeField]
        private AttributeSO moveSpeedAttribute = default!;

        [SerializeField]
        private bool showDebugHud = true;

        private SampleCharacterEntity entity = default!;

        private void Awake()
        {
            entity = GetComponent<SampleCharacterEntity>();
            if (characterDefinition != null)
            {
                entity.InitializeFromDefinition(new InstanceId(0), characterDefinition);
            }
        }

        private void Start()
        {
            LogEffectiveStats("Initial");
            if (healthAttribute != null)
            {
                var bonusHealth = new FloatAttributeValue { Value = 25f };
                var healthMod = new EntityModifierEntry(healthAttribute, bonusHealth);
                entity.AddModifier(healthMod);
            }

            LogEffectiveStats("After +25 health modifier (numeric slots are summed)");
        }

        private void LogEffectiveStats(string label)
        {
            if (healthAttribute != null &&
                entity.TryGetAttribute(healthAttribute, out AttributeValue health))
            {
                Debug.Log($"[Entities Sample] {label} — Health effective: {FormatAttributeText(health)}", this);
            }

            if (moveSpeedAttribute != null &&
                entity.TryGetAttribute(moveSpeedAttribute, out AttributeValue speed))
            {
                Debug.Log($"[Entities Sample] {label} — Move Speed effective: {FormatAttributeText(speed)}", this);
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
            DrawAttributeLine("Health (effective)", healthAttribute);
            DrawAttributeLine("Move speed (effective)", moveSpeedAttribute);
            GUILayout.EndArea();
        }

        private void DrawAttributeLine(string label, AttributeSO slot)
        {
            if (slot != null && entity.TryGetAttribute(slot, out AttributeValue value))
            {
                GUILayout.Label($"{label}: {FormatAttributeText(value)}");
            }
        }

        private string FormatAttributeText(AttributeValue value)
        {
            return value switch
            {
                FloatAttributeValue f => f.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                IntAttributeValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BoolAttributeValue b => b.Value.ToString(),
                StringAttributeValue s => s.Value,
                _ => value?.ToString() ?? "(null)"
            };
        }
    }
}

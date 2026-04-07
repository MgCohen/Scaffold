using UnityEngine;

namespace Scaffold.Entities.Samples
{
    /// <summary>
    /// Wires an <see cref="Entity"/> from a <see cref="SampleCharacterDefinition"/>, logs modifier combination, and shows a small HUD.
    /// Add to a GameObject together with <see cref="SampleCharacterBehaviorRunner"/> and input/behaviors (see sample prefab).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Entity))]
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

        private Entity entity = default!;

        private void Awake()
        {
            entity = GetComponent<Entity>();
            if (characterDefinition != null)
            {
                entity.InitializeFromDefinition(characterDefinition);
            }
        }

        private void Start()
        {
            LogEffectiveStats("Initial");
            if (healthAttribute != null)
            {
                entity.AddModifier(new EntityModifierEntry(healthAttribute, "25"));
            }

            LogEffectiveStats("After +25 health modifier (numeric slots are summed)");
        }

        private void LogEffectiveStats(string label)
        {
            if (healthAttribute != null &&
                entity.TryGetAttribute(healthAttribute, out Attribute health))
            {
                Debug.Log($"[Entities Sample] {label} — Health effective payload: {health.Payload}", this);
            }

            if (moveSpeedAttribute != null &&
                entity.TryGetAttribute(moveSpeedAttribute, out Attribute speed))
            {
                Debug.Log($"[Entities Sample] {label} — Move Speed effective payload: {speed.Payload}", this);
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
            if (slot != null && entity.TryGetAttribute(slot, out Attribute value))
            {
                GUILayout.Label($"{label}: {value.Payload}");
            }
        }
    }
}

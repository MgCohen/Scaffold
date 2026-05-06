using System;
using Scaffold.Types;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Spike.Editor
{
    /// <summary>
    /// Phase 2a spike — does <see cref="Scaffold.Types.TypeReference"/>'s IMGUI custom drawer
    /// render inside GraphToolkit's UIToolkit option UI?
    /// <para>If yes: we can use <c>AddOption&lt;TypeReference&gt;</c> in OnTrigger / Return to
    /// give users a real type picker, persisted via TypeReference's JSON-string roundtrip
    /// (avoids AQN brittleness).</para>
    /// <para>If no (renders as a default text/object field): we'll need a custom UIToolkit
    /// option provider for type picking, or fall back to a per-package generated enum.</para>
    /// </summary>
    [Serializable]
    public sealed class SpikeTypeNode : Node
    {
        const string TypeOptionName = "PickedType";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<TypeReference>(TypeOptionName)
                .WithDisplayName("Picked Type")
                .WithTooltip("Phase 2a — does TypeReference's drawer render in the option UI?");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // Always emit one output so the node is visibly distinct in the graph.
            context.AddOutputPort<int>("DummyOut").Build();

            var opt = GetNodeOptionByName(TypeOptionName);
            UnityEngine.Debug.Log($"[SpikeTypeNode.OnDefinePorts] called. opt is null? {opt == null}");
            if (opt == null) return;

            var hasValue = opt.TryGetValue<TypeReference>(out var tr);
            UnityEngine.Debug.Log($"[SpikeTypeNode.OnDefinePorts] hasValue={hasValue}, tr is null? {tr == null}, tr.Type is null? {tr?.Type == null}");

            // Reflection peek into the private serializedType field — to see whether GT round-tripped
            // it at all, or if even the JSON is missing.
            if (tr != null)
            {
                var serField = typeof(TypeReference).GetField("serializedType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var serValue = serField?.GetValue(tr) as string;
                UnityEngine.Debug.Log($"[SpikeTypeNode.OnDefinePorts] serializedType field = '{(serValue ?? "<null>")}'");
            }

            // Try the manual deserialize, with try/catch so we see exceptions.
            if (tr != null && tr.Type == null)
            {
                try
                {
                    tr.OnAfterDeserialize();
                    UnityEngine.Debug.Log($"[SpikeTypeNode.OnDefinePorts] after OnAfterDeserialize: tr.Type is null? {tr.Type == null}, tr.Type.Name={tr.Type?.Name}");
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[SpikeTypeNode.OnDefinePorts] OnAfterDeserialize threw: {ex}");
                }
            }

            if (tr?.Type == null) return;

            foreach (var f in tr.Type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (f.FieldType == typeof(int))
                    context.AddOutputPort<int>(f.Name).Build();
                else if (f.FieldType == typeof(string))
                    context.AddOutputPort<string>(f.Name).Build();
                else if (f.FieldType == typeof(bool))
                    context.AddOutputPort<bool>(f.Name).Build();
                else
                    context.AddOutputPort(f.Name).Build();
            }
        }
    }
}

using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Scaffold.GraphFlow.Spike.Editor
{
    /// <summary>
    /// Phase 3 spike — does <c>[SerializeField] [SerializeReference]</c> on a <c>Node</c> subclass
    /// field survive a save → close → reopen cycle?
    /// <para>If yes: production OnTrigger / Return store their baked closed-generic instance
    /// directly on the editor mirror, no per-load reflection.</para>
    /// <para>If no: we bake into the imported <c>GraphAsset&lt;&gt;</c> instead during
    /// <c>OnImportAsset</c>; same end result, more plumbing.</para>
    /// </summary>
    [Serializable]
    public sealed class SpikeBakeNode : Node
    {
        const string EventOptionName = "EventType";

        [SerializeField, SerializeReference, HideInInspector]
        SpikeBakedShape baked;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<SpikeEventChoice>(EventOptionName)
                .WithDisplayName("Event Type")
                .WithDefaultValue(SpikeEventChoice.None);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // Always emit one stable port so the node has a visible identity.
            context.AddOutputPort<int>("DummyOut").Build();

            // Read the option, bake an instance into the polymorphic field. In production this
            // bake step would be a proper edit-time action (option-change handler or a Save hook),
            // not buried inside OnDefinePorts — but for the spike we just want to verify the
            // [SerializeReference] field roundtrips. Doing the bake here keeps the spike simple.
            var opt = GetNodeOptionByName(EventOptionName);
            if (opt != null && opt.TryGetValue<SpikeEventChoice>(out var choice))
            {
                Bake(choice);
            }

            // Diagnostic — what's currently in `baked`?
            // After save → close → reopen, this log on first OnDefinePorts call should show the
            // SAME baked instance type and field values as before save. That proves [SerializeReference]
            // survives the GraphToolkit serialization roundtrip.
            var describeLog = baked == null
                ? "<null>"
                : $"{baked.GetType().Name} → {baked.Describe()}";
            Debug.Log($"[SpikeBakeNode.OnDefinePorts] baked = {describeLog}");
        }

        void Bake(SpikeEventChoice choice)
        {
            baked = choice switch
            {
                SpikeEventChoice.SpikeDamage => new SpikeBakedDamageShape
                {
                    PickedTypeName = nameof(SpikeDamage),
                    FieldCount = 3,
                },
                SpikeEventChoice.SpikeHeal => new SpikeBakedHealShape
                {
                    Note = "healed by spike",
                },
                _ => null,
            };
        }
    }
}

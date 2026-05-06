using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Spike.Editor
{
    /// <summary>
    /// Phase 1 spike — does <c>AddOption&lt;TEnum&gt;</c> give us a real dropdown, and does
    /// <c>OnDefinePorts</c> re-run when the option changes?
    /// <para>The shape of <see cref="OnTriggerEditorNode"/> already uses <c>AddOption&lt;Timing&gt;</c>
    /// so this is structurally similar — but OnTrigger has been broken/never-tested in our setup,
    /// so we validate the mechanism on a clean throwaway node first.</para>
    /// </summary>
    [Serializable]
    public sealed class SpikeOptionsNode : Node
    {
        const string ModeOptionName = "Mode";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<SpikeMode>(ModeOptionName)
                .WithDisplayName("Mode")
                .WithTooltip("Phase 1 spike — switching this should change the node's ports.")
                .WithDefaultValue(SpikeMode.Read);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            var modeOpt = GetNodeOptionByName(ModeOptionName);
            if (modeOpt == null || !modeOpt.TryGetValue<SpikeMode>(out var mode))
            {
                return;
            }

            switch (mode)
            {
                case SpikeMode.Read:
                    context.AddOutputPort<int>("ReadOut").Build();
                    break;
                case SpikeMode.Write:
                    context.AddInputPort<int>("WriteIn").Build();
                    break;
                case SpikeMode.ReadWrite:
                    context.AddOutputPort<int>("ReadOut").Build();
                    context.AddInputPort<int>("WriteIn").Build();
                    break;
            }
        }
    }

    /// <summary>Three values so the dropdown UI is obviously a dropdown (>2 entries).</summary>
    public enum SpikeMode
    {
        Read,
        Write,
        ReadWrite,
    }
}

using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Spike.Editor
{
    /// <summary>
    /// Phase 0 spike — does GraphToolkit's same-asm auto-discovery rule work for our package?
    /// <para>This file lives in <c>Scaffold.GraphFlow.Spike.Editor</c>, the same asm where the
    /// package generator emits <c>SpikeGraph</c>. Per the GraphToolkit docs:
    /// <i>"By default, nodes defined in the same assembly as the graph are considered compatible
    /// and available."</i> So this node should appear in the Add Node menu of any Spike graph
    /// without further ceremony — no <c>[UseWithGraph]</c>, no registration call, no
    /// per-graph allow-list.</para>
    /// <para>If it appears: discovery is solved, move on to Phase 1 (enum dropdown option).</para>
    /// <para>If it doesn't: the rule isn't actually that simple. We dig further.</para>
    /// </summary>
    [Serializable]
    public sealed class SpikeNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<int>("In").Build();
            context.AddOutputPort<int>("Out").Build();
        }
    }
}

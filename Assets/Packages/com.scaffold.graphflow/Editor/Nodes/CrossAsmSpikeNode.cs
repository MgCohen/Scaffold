using System;
using Scaffold.GraphFlow.Editor.GToolkit;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor.Nodes
{
    /// <summary>
    /// Phase 0.5 spike — does <see cref="UseWithGraphAttribute"/> with the non-generic
    /// <see cref="GraphFlowGraph"/> anchor expose this node to every downstream package's graphs?
    /// <para>This file lives in <c>Scaffold.GraphFlow.Editor</c> (the shared package editor asm),
    /// NOT in the per-package editor asm where the consumer's Graph subclass is generator-emitted.
    /// If GraphToolkit's <c>IsGraphTypeSupported</c> walks the inheritance chain (which the source
    /// shows it does — it's <c>Type.IsAssignableFrom</c>), this node should appear in every
    /// <see cref="Graph{TRunner}"/>-derived graph because each consumer's graph has
    /// <see cref="GraphFlowGraph"/> in its inheritance chain.</para>
    /// <para>If it appears in the SpikeGraph's Add Node menu: the inheritance trick works, OnTrigger
    /// and Return can use the same pattern in production — no per-package shim generation needed.</para>
    /// </summary>
    [Serializable]
    [UseWithGraph(typeof(GraphFlowGraph))]
    public sealed class CrossAsmSpikeNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<int>("CrossIn").Build();
            context.AddOutputPort<int>("CrossOut").Build();
        }
    }
}

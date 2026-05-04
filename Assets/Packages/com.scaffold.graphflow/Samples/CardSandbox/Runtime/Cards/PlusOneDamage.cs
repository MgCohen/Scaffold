#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    /// <summary>"+1 damage" trigger — OnTrigger&lt;DamageDealt&gt; (Before) → bumps Amount by 1.</summary>
    public static class PlusOneDamage
    {
        public static CardEffectGraphAsset BuildAsset()
        {
            var entry = new OnTrigger<DamageDealt>
            {
                nodeId = 1, editorGuid = "plusone-trigger", Timing = Timing.Before,
            };
            var mutator = new PlusOneDamageMutator
            {
                nodeId = 2, editorGuid = "plusone-mutator", Source = entry,
            };

            var asset = ScriptableObject.CreateInstance<CardEffectGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, mutator };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnTrigger<DamageDealt>).AssemblyQualifiedName!, rootNodeId = 1 },
            };
            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1, fromFlowPortName = "FlowOut",
                toNodeId = 2, toFlowPortName = "FlowIn",
            });
            return asset;
        }
    }

    /// <summary>PlusOneDamage's effect node — bumps the in-flight DamageDealt event's Amount by 1.</summary>
    public sealed class PlusOneDamageMutator : RuntimeNode<CardEffectRunner>
    {
        public OnTrigger<DamageDealt>? Source;

        public override Task Execute(CardEffectRunner runner, Flow flow)
        {
            if (Source?.Event != null) Source.Event.Amount += 1;
            return flow.Stop();
        }
    }
}

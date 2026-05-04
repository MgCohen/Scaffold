#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    /// <summary>
    /// Trigger card. The graph entry is <c>OnTrigger&lt;DamageDealt&gt;</c> (built-in primitive)
    /// configured for <see cref="Timing.Before"/>; on each Pre-damage event the host's wiring loop
    /// hands the event to the trigger, whose flow walks into <see cref="MutateAmountNode"/> to add
    /// 1 to the in-flight damage.
    /// </summary>
    public static class PlusOneDamage
    {
        /// <summary>Tiny runtime node that reads the trigger's per-event payload and bumps Amount.
        /// Hand-authored on purpose — phase 5 sweeps this once the package ships a generic
        /// "modify event field" primitive.</summary>
        public sealed class MutateAmountNode : RuntimeNode<CardEffectRunner>
        {
            public const string FlowInPortName = "FlowIn";

            public OnTrigger<DamageDealt>? Source;

            public override Task Execute(CardEffectRunner runner, Flow flow)
            {
                if (Source?.Event != null) Source.Event.Amount += 1;
                return flow.Stop();
            }
        }

        public static CardEffectGraphAsset BuildAsset()
        {
            var entry = new OnTrigger<DamageDealt>
            {
                nodeId = 1,
                editorGuid = "plusone-trigger",
                Timing = Timing.Before,
            };
            var mutator = new MutateAmountNode
            {
                nodeId = 2,
                editorGuid = "plusone-mutator",
                Source = entry,
            };

            var asset = ScriptableObject.CreateInstance<CardEffectGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, mutator };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex
                {
                    entryTypeId = typeof(OnTrigger<DamageDealt>).AssemblyQualifiedName!,
                    rootNodeId = 1,
                },
            };
            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1, fromFlowPortName = OnTrigger<DamageDealt>.FlowOutPortName,
                toNodeId = 2, toFlowPortName = MutateAmountNode.FlowInPortName,
            });
            return asset;
        }
    }
}

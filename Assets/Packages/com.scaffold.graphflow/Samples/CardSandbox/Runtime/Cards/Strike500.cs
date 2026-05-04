#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    /// <summary>"Strike 5" — OnPlay → DealDamage(5).</summary>
    public static class Strike500
    {
        public const int BaseDamage = 5;

        public static CardEffectGraphAsset BuildAsset()
        {
            var entry = new OnPlayRuntime { nodeId = 1, editorGuid = "strike500-entry" };
            var dispatcher = new Strike500Dispatcher { nodeId = 2, editorGuid = "strike500-dispatch" };

            var asset = ScriptableObject.CreateInstance<CardEffectGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, dispatcher };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };
            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1, fromFlowPortName = "FlowOut",
                toNodeId = 2, toFlowPortName = "FlowIn",
            });
            return asset;
        }
    }

    /// <summary>Strike500's effect node — runs DealDamageCommand with the card's BaseDamage.</summary>
    public sealed class Strike500Dispatcher : RuntimeNode<CardEffectRunner>
    {
        public override async Task Execute(CardEffectRunner runner, Flow flow)
        {
            var scope = (ICardEffectScope)flow.Scope!;
            var cmd = new DealDamageCommand { Amount = Strike500.BaseDamage };
            await cmd.Execute(scope, flow).ConfigureAwait(false);
            await flow.Stop().ConfigureAwait(false);
        }
    }
}

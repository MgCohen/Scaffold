#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    /// <summary>
    /// Trigger card. Subscribes to <see cref="PreDamageDealtEvent"/> via the host's entry-catalog
    /// wiring loop and mutates <c>Amount += 1</c> in flight.
    /// </summary>
    public static class PlusOneDamage
    {
        public sealed class OnPreDamageEntry : EntryRuntimeNode<PreDamageDealtEvent>
        {
            public override Task Execute(Flow flow)
            {
                if (Payload != null) Payload.Amount += 1;
                return flow.Stop();
            }
        }

        public static CardEffectGraphAsset BuildAsset()
        {
            var entry = new OnPreDamageEntry { nodeId = 1, editorGuid = "plusone-entry" };

            var asset = ScriptableObject.CreateInstance<CardEffectGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex
                {
                    entryTypeId = typeof(PreDamageDealtEvent).AssemblyQualifiedName!,
                    rootNodeId = 1,
                },
            };
            return asset;
        }
    }
}

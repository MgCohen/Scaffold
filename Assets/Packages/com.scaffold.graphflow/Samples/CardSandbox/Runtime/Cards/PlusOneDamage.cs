#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    /// <summary>
    /// Trigger card. Subscribes to <see cref="PreDamageDealtEvent"/> via the host's entry-catalog
    /// wiring loop and mutates <c>Amount += 1</c> in flight, before <see cref="DealDamageCommand"/>
    /// reads it back to apply damage. The unified entry-and-trigger model — same node shape as
    /// Strike500.OnPlayEntry, just with a different payload type.
    /// </summary>
    public static class PlusOneDamage
    {
        public sealed class OnPreDamageEntry : EntryRuntimeNode<PreDamageDealtEvent, CardEffectRunner, Unit>
        {
            public override Task Execute(CardEffectRunner runner, Flow flow)
            {
                if (Payload != null) Payload.Amount += 1;
                return flow.Stop();
            }
        }

        public static CardSandboxAsset BuildAsset()
        {
            var entry = new OnPreDamageEntry { nodeId = 1, editorGuid = "plusone-entry" };

            var asset = ScriptableObject.CreateInstance<CardSandboxAsset>();
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

#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    /// <summary>
    /// "Strike 5" card. OnPlay entry → DealDamageDispatcher with Amount = 5.
    /// </summary>
    public static class Strike500
    {
        public const int BaseDamage = 5;

        /// <summary>Entry runtime — emits <see cref="OnPlay"/> walks into the dispatcher.</summary>
        public sealed class OnPlayEntry : EntryRuntimeNode<OnPlay>
        {
            public const string FlowOutPortName = "FlowOut";

            public override Task Execute(Flow flow) =>
                flow.GoTo(FlowOutPortName);
        }

        /// <summary>Dispatcher runtime — runs the DealDamageCommand with a fixed Amount.</summary>
        public sealed class DealDamageDispatcher : RuntimeNode<CardEffectRunner>
        {
            public const string FlowInPortName = "FlowIn";

            public int Amount = BaseDamage;

            public override async Task Execute(CardEffectRunner runner, Flow flow)
            {
                var scope = (ICardEffectScope)flow.Scope!;
                var cmd = new DealDamageCommand { Amount = Amount, Target = (flow.Scope as ICardEffectScope)?.Damage };
                await cmd.Execute(scope, flow).ConfigureAwait(false);
                await flow.Stop().ConfigureAwait(false);
            }
        }

        /// <summary>Constructs the GraphAsset with OnPlay → DealDamage wired up.</summary>
        public static CardEffectGraphAsset BuildAsset()
        {
            var entry = new OnPlayEntry { nodeId = 1, editorGuid = "strike500-entry" };
            var dispatcher = new DealDamageDispatcher { nodeId = 2, editorGuid = "strike500-dispatch" };

            var asset = ScriptableObject.CreateInstance<CardEffectGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, dispatcher };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };
            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1, fromFlowPortName = OnPlayEntry.FlowOutPortName,
                toNodeId = 2, toFlowPortName = DealDamageDispatcher.FlowInPortName,
            });
            return asset;
        }
    }

    /// <summary>
    /// Mode-2 command — publishes one <see cref="DamageDealt"/> event twice around applying damage:
    /// once with <see cref="Timing.Before"/> (so triggers may mutate <c>Amount</c>) and once with
    /// <see cref="Timing.After"/> (reactive triggers).
    /// </summary>
    [GraphCommandPair(ResultType = typeof(Unit))]
    public sealed class DealDamageCommand : Command<Unit>
    {
        [GraphPort]
        public int Amount;

        public object? Target;

        public override async Task<Unit> Execute(ICardEffectScope scope, Flow flow)
        {
            var evt = new DamageDealt { Amount = Amount, Target = Target };
            await scope.Bus.Publish(evt, Timing.Before).ConfigureAwait(false);

            scope.Damage.Apply(evt.Target, evt.Amount);

            await scope.Bus.Publish(evt, Timing.After).ConfigureAwait(false);
            return Unit.Default;
        }
    }
}

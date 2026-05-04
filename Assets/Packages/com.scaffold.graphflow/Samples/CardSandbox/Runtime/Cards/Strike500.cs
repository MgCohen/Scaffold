#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    /// <summary>
    /// "Strike 5" card. OnPlay entry → DealDamageDispatcher with Amount = 5.
    /// Hand-authored runtime nodes; no .gfasset / generator involvement (sample is runtime-only).
    /// </summary>
    public static class Strike500
    {
        public const int BaseDamage = 5;

        /// <summary>Entry runtime — emits <see cref="OnPlay"/> walks into the dispatcher.</summary>
        public sealed class OnPlayEntry : EntryRuntimeNode<OnPlay, CardEffectRunner>
        {
            public const int FlowOutPortId = unchecked((int)0xC0010001u);

            public override Task Execute(CardEffectRunner runner, Flow flow) =>
                flow.GoTo(FlowOutPortId);
        }

        /// <summary>Dispatcher runtime — runs the DealDamageCommand with a fixed Amount.</summary>
        public sealed class DealDamageDispatcher : RuntimeNode<CardEffectRunner>
        {
            public const int FlowInPortId = 0;

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
                fromNodeId = 1, fromFlowPortId = OnPlayEntry.FlowOutPortId,
                toNodeId = 2, toFlowPortId = DealDamageDispatcher.FlowInPortId,
            });
            return asset;
        }
    }

    /// <summary>
    /// Mode-2 command — publishes Pre/Post damage events around applying damage to the scope's sink.
    /// Triggers on PreDamageDealtEvent (e.g. PlusOneDamage) can mutate Amount before it lands.
    /// <para>The <see cref="GraphCommandPairAttribute"/> + <see cref="GraphPortAttribute"/> on
    /// <see cref="Amount"/> opt the type into the generator's Mode-2 emit, producing
    /// <c>DealDamageCommandDispatcherRuntime : CardCommandDispatcher&lt;DealDamageCommand, Unit&gt;</c>
    /// (runtime asm) and <c>DealDamageCommandDispatcherEditorNode</c> (editor asm). Result type is
    /// <see cref="Unit"/> so no output ports are emitted. <see cref="Target"/> has no port id —
    /// visually authored graphs leave it null; the runtime path through Strike500's hand-authored
    /// dispatcher still sets it explicitly.</para>
    /// </summary>
    [GraphCommandPair(
        ResultType = typeof(Unit),
        FlowInPortId = unchecked((int)0xC1D0_0001u),
        FlowOutPortId = unchecked((int)0xC1D0_0002u))]
    public sealed class DealDamageCommand : Command<Unit>
    {
        [GraphPort(Id = unchecked((int)0xC1D0_1001u))]
        public int Amount;

        public object? Target;

        public override async Task<Unit> Execute(ICardEffectScope scope, Flow flow)
        {
            var pre = new PreDamageDealtEvent { Amount = Amount, Target = Target };
            await scope.Bus.Publish(pre).ConfigureAwait(false);

            scope.Damage.Apply(pre.Target, pre.Amount);

            await scope.Bus.Publish(new DamageDealtEvent { FinalAmount = pre.Amount, Target = pre.Target })
                .ConfigureAwait(false);
            return Unit.Default;
        }
    }
}

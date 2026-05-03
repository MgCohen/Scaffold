#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.CardSandbox.Cards;
using Scaffold.GraphFlow.M0;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Tests
{
    /// <summary>
    /// M3-prep validation: drives a hand-built graph (Strike500 entry → DealDamage dispatcher)
    /// through the CardSandbox stand-in pipeline. Asserts the pipeline produces the base damage
    /// alone, and that a registered listener can rewrite the result before it returns to the graph.
    /// </summary>
    public sealed class Strike500Tests
    {
        sealed class TestScope : IEffectScope
        {
            public CancellationToken CancellationToken { get; set; }
            public T? Get<T>() where T : class => default;
            public string Reason { get; set; } = "test";
        }

        sealed class PlusOneDamageListener : ICommandListener<DealDamageCommand, DamageResult>
        {
            public async Task<DamageResult> Intercept(DealDamageCommand command, IEffectScope scope, CommandNext<DamageResult> next)
            {
                var inner = await next().ConfigureAwait(false);
                return new DamageResult { DamageDealt = inner.DamageDealt + 1 };
            }
        }

        static async Task<int> RunStrike500(CardEffectRunner runner)
        {
            var entry      = new Strike500EntryRuntime { nodeId = 1, editorGuid = "entry" };
            var dispatcher = new DealDamageDispatcherRuntime { nodeId = 2, editorGuid = "dispatch" };

            var asset = ScriptableObject.CreateInstance<CardSandboxAsset>();
            asset.nodes = new List<RuntimeNode> { entry, dispatcher };
            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1, fromFlowPortId = Strike500EntryRuntime.FlowOutPortId,
                toNodeId = 2, toFlowPortId = DealDamageDispatcherRuntime.FlowInPortIdConst,
            });
            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1, fromPortId = Strike500EntryRuntime.BaseDamagePort,
                toNodeId = 2, toPortId = DealDamageDispatcherRuntime.AmountPortId,
            });

            // Hand-hydrate the data wire (mirrors what GraphController.Initialize would do).
            dispatcher.Bind(
                DealDamageDispatcherRuntime.AmountPortId,
                entry,
                Strike500EntryRuntime.BaseDamagePort);

            entry.SetPayload(new Strike500 { BaseDamage = 5 });

            await new GraphExecutor<CardEffectRunner>().RunFlow(entry, runner, asset);

            return dispatcher.DamageDealt.Read();
        }

        [Test]
        public async Task Strike500_NoListeners_DealsBaseDamage()
        {
            var runner = new CardEffectRunner(new TestScope());
            var dealt = await RunStrike500(runner);
            Assert.AreEqual(5, dealt);
        }

        [Test]
        public async Task Strike500_PlusOneListener_DealsBumpedDamage()
        {
            var runner = new CardEffectRunner(new TestScope());
            runner.Pipeline.Register<DealDamageCommand, DamageResult>(new PlusOneDamageListener());
            var dealt = await RunStrike500(runner);
            Assert.AreEqual(6, dealt);
        }
    }
}

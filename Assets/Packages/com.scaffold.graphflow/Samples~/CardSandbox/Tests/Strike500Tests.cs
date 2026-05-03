#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.CardSandbox.Cards;

namespace Scaffold.GraphFlow.CardSandbox.Tests
{
    /// <summary>
    /// M3 D8/D9 validation: drives hand-built card graphs through the entry-catalog + event-bus model.
    /// Replaces the M2-prep CommandPipeline-based tests. Cross-card command modification now happens
    /// through events + trigger entries wired by the host via <c>controller.EntryNodes</c>.
    /// </summary>
    public sealed class Strike500Tests
    {
        [Test]
        public async Task Strike500_Solo_Deals_5_Damage()
        {
            var bus = new EventBus();
            var runner = new CardEffectRunner(bus);
            var sink = new DamageSink();

            var asset = Strike500.BuildAsset();
            var controller = new GraphController<CardEffectRunner>(asset);
            controller.Initialize(runner, () => new CardEffectScope(bus, sink));

            await controller.Run<OnPlay, Unit>(new OnPlay());

            Assert.AreEqual(5, sink.LastAmount);
        }

        [Test]
        public async Task Strike500_Plus_PlusOneDamage_Trigger_Deals_6_Damage()
        {
            var bus = new EventBus();
            var runner = new CardEffectRunner(bus);
            var sink = new DamageSink();

            var s500 = new GraphController<CardEffectRunner>(Strike500.BuildAsset());
            var p1d = new GraphController<CardEffectRunner>(PlusOneDamage.BuildAsset());
            s500.Initialize(runner, () => new CardEffectScope(bus, sink));
            p1d.Initialize(runner, () => new CardEffectScope(bus, sink));

            // Wire triggers via the entry-catalog pattern (D9). Pattern-match the typed entry, subscribe
            // to the bus. Imperative entries (OnPlay) aren't auto-subscribed — host calls them directly.
            foreach (var card in new[] { s500, p1d })
            foreach (var entry in card.EntryNodes)
            {
                switch (entry)
                {
                    case EntryRuntimeNode<PreDamageDealtEvent, CardEffectRunner, Unit> trig:
                        bus.Subscribe<PreDamageDealtEvent>(async e => await trig.Run(e));
                        break;
                }
            }

            await s500.Run<OnPlay, Unit>(new OnPlay());

            Assert.AreEqual(6, sink.LastAmount);
        }
    }
}

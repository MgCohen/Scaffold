#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.CardSandbox.Cards;

namespace Scaffold.GraphFlow.CardSandbox.Tests
{
    /// <summary>
    /// Post-M3 phase 3 validation: trigger entries are now <c>OnTrigger&lt;DamageDealt&gt;</c>
    /// instances (built-in primitive). Host pattern-matches the typed runtime node, reads its
    /// configured <see cref="Timing"/>, subscribes to the bus accordingly, and on each delivery
    /// constructs a fresh OnTrigger payload carrying the event reference.
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

            await controller.Run(new OnPlay());

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

            // Wire triggers via the entry-catalog pattern. Pattern-match the typed
            // OnTrigger<DamageDealt> entry, read its configured Timing, subscribe to the bus
            // accordingly. Each delivery constructs a fresh OnTrigger payload carrying the live
            // event reference + the same Timing.
            foreach (var card in new[] { s500, p1d })
            foreach (var entry in card.EntryNodes)
            {
                switch (entry)
                {
                    case OnTrigger<DamageDealt> trig:
                        bus.Subscribe<DamageDealt>(
                            async e => await trig.Run(new OnTrigger<DamageDealt> { Event = e, Timing = trig.Timing }),
                            trig.Timing);
                        break;
                }
            }

            await s500.Run(new OnPlay());

            Assert.AreEqual(6, sink.LastAmount);
        }
    }
}

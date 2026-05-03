#nullable enable
using System;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Mode-2 runner for the card sandbox. Holds long-lived host services (here just the EventBus
    /// reference). The per-run scope (<see cref="ICardEffectScope"/>) is constructed by the
    /// controller's scope-factory and lives on <see cref="Flow.Scope"/>, NOT on the runner.
    /// </summary>
    public sealed class CardEffectRunner : GraphRunner
    {
        public EventBus Bus { get; }

        public CardEffectRunner(EventBus bus)
        {
            Bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }
    }
}

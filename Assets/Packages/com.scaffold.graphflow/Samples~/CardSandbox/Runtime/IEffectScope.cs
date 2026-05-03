#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// CardSandbox per-run scope. Inherits the package's empty <see cref="Scaffold.GraphFlow.IEffectScope"/>
    /// marker so it can ride on <see cref="Flow.Scope"/>; adds the members the sample's dispatchers
    /// actually need (event bus reference + the damage sink they write into).
    ///
    /// <para>Renamed from the M2-prep <c>IEffectScope</c> to <c>ICardEffectScope</c> to avoid colliding
    /// with the package's marker of the same name. Production hosts would shape this however their
    /// dispatcher commands need.</para>
    /// </summary>
    public interface ICardEffectScope : Scaffold.GraphFlow.IEffectScope
    {
        EventBus Bus { get; }
        DamageSink Damage { get; }
    }

    /// <summary>
    /// Stand-in for whatever the host writes damage into. Tests assert against
    /// <see cref="LastAmount"/> after a card's flow completes.
    /// </summary>
    public sealed class DamageSink
    {
        public int LastAmount { get; private set; }
        public object? LastTarget { get; private set; }

        public void Apply(object? target, int amount)
        {
            LastTarget = target;
            LastAmount = amount;
        }
    }

    /// <summary>Concrete sample scope — wraps the runner's EventBus and a damage sink.</summary>
    public sealed class CardEffectScope : ICardEffectScope
    {
        public EventBus Bus { get; }
        public DamageSink Damage { get; }

        public CardEffectScope(EventBus bus, DamageSink damage)
        {
            Bus = bus;
            Damage = damage;
        }
    }
}

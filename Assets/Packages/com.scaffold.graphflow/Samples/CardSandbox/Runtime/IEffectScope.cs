#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// CardSandbox per-run scope. Rides on <see cref="Flow.Scope"/> (typed as <c>object?</c>
    /// at the framework level); dispatcher nodes downcast to this interface to reach the
    /// sample's services (event bus + damage sink).
    /// </summary>
    public interface ICardEffectScope
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

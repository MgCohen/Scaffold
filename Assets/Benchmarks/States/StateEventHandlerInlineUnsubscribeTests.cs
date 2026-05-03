using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Samples;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Audit §4.11 — <c>StateEventHandler.NotifyReferenceSubscriptions</c> iterates the
    /// subscription list directly. A subscriber that calls <c>Unsubscribe</c> from inside its own
    /// callback mutates the list mid-iteration, raising
    /// <see cref="System.InvalidOperationException"/> ("Collection was modified").
    /// Pre-Phase-2 this test reproduces the throw; Phase 2's snapshot-then-iterate fix turns it
    /// green.
    /// </summary>
    [Ignore("Expected red until com.scaffold.states-refactor Phase 2 lands.")]
    public sealed class StateEventHandlerInlineUnsubscribeTests
    {
        [Test]
        public void Notify_SubscriberUnsubscribesItself_DoesNotThrow()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            int fired = 0;
            System.Action<IReference, CounterState, StateChangeEvent> selfCancel = null!;
            selfCancel = (_, _, _) =>
            {
                fired++;
                store.Unsubscribe(Reference.Null, selfCancel);
            };

            // Two subscribers — the first unsubscribes itself, the second must still fire and
            // not be skipped because of the list mutation.
            int otherFired = 0;
            store.Subscribe(Reference.Null, selfCancel);
            store.Subscribe<CounterState>(Reference.Null, (_, _, _) => otherFired++);

            CounterState payload = new(1);
            Assert.DoesNotThrow(() => store.Events.Notify(Reference.Null, payload, StateChangeEvent.Updated));
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(otherFired, Is.EqualTo(1));
        }
    }
}

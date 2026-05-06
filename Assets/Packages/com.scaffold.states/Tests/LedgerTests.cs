#nullable enable

using System;
using NUnit.Framework;
using Scaffold.States;

namespace Scaffold.States.Tests
{
    public sealed class LedgerTests
    {
        [Test]
        public void Add_ThenGet_ReturnsSubscriptionList()
        {
            var ledger = new Ledger();
            var sub = new TypedSubscription<CounterState>((Action<Reference, CounterState, StateChangeEvent>)((_, _, _) => { }));

            ledger.Add(sub);

            var result = ledger.Get(typeof(CounterState));
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
        }

        [Test]
        public void Get_UnknownType_ReturnsNull()
        {
            var ledger = new Ledger();

            var result = ledger.Get(typeof(CounterState));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void RemoveSubscription_ExistingSubscription_ReturnsTrue()
        {
            var ledger = new Ledger();
            Action<Reference, CounterState, StateChangeEvent> action = (_, _, _) => { };
            ledger.Add(new TypedSubscription<CounterState>(action));

            bool removed = ledger.RemoveSubscription<CounterState>(action);

            Assert.That(removed, Is.True);
            Assert.That(ledger.Get(typeof(CounterState)), Is.Null);
        }

        [Test]
        public void RemoveSubscription_MissingType_ReturnsFalse()
        {
            var ledger = new Ledger();
            Action<Reference, CounterState, StateChangeEvent> action = (_, _, _) => { };

            bool removed = ledger.RemoveSubscription<CounterState>(action);

            Assert.That(removed, Is.False);
        }

        [Test]
        public void RemoveSubscription_WrongActionInstance_ReturnsFalse()
        {
            var ledger = new Ledger();
            Action<Reference, CounterState, StateChangeEvent> original = (_, _, _) => { };
            Action<Reference, CounterState, StateChangeEvent> different = (_, _, _) => { };
            ledger.Add(new TypedSubscription<CounterState>(original));

            bool removed = ledger.RemoveSubscription<CounterState>(different);

            Assert.That(removed, Is.False);
        }

        [Test]
        public void Add_MultipleSameType_AllRetrieved()
        {
            var ledger = new Ledger();
            ledger.Add(new TypedSubscription<CounterState>((Action<Reference, CounterState, StateChangeEvent>)((_, _, _) => { })));
            ledger.Add(new TypedSubscription<CounterState>((Action<Reference, CounterState, StateChangeEvent>)((_, _, _) => { })));

            var result = ledger.Get(typeof(CounterState));

            Assert.That(result, Has.Count.EqualTo(2));
        }

        [Test]
        public void RemoveSubscription_LastEntry_CleansUpLookup()
        {
            var ledger = new Ledger();
            Action<Reference, CounterState, StateChangeEvent> action = (_, _, _) => { };
            ledger.Add(new TypedSubscription<CounterState>(action));

            ledger.RemoveSubscription<CounterState>(action);

            Assert.That(ledger.Lookup, Does.Not.ContainKey(typeof(CounterState)));
        }

        private sealed record CounterState(int Value) : State;
    }
}

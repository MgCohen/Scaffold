#nullable enable

using System;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StateEventHandlerInlineUnsubscribeTests
    {
        [Test]
        public void Notify_SubscriberUnsubscribesItself_DoesNotThrow()
        {
            var env = InlineUnsubscribeTestEnv.Create();
            CounterState payload = new(1);
            Assert.DoesNotThrow(() => env.Store.Events.Notify(Reference.Null, payload, StateChangeEvent.Updated));
            Assert.That(env.FirstFired, Is.EqualTo(1));
            Assert.That(env.SecondFired, Is.EqualTo(1));
        }

        private sealed class InlineUnsubscribeTestEnv
        {
            private InlineUnsubscribeTestEnv()
            {
            }

            public Store Store { get; private set; } = null!;

            public int FirstFired { get; private set; }

            public int SecondFired { get; private set; }

            public static InlineUnsubscribeTestEnv Create()
            {
                var env = new InlineUnsubscribeTestEnv();
                var builder = new StoreBuilder();
                builder.AddState(new CounterState(0));
                env.Store = builder.Build();
                Action<Reference, CounterState, StateChangeEvent> selfCancel = null!;
                selfCancel = (_, _, _) => { env.FirstFired++; env.Store.Unsubscribe(Reference.Null, selfCancel); };

                env.Store.Subscribe(Reference.Null, selfCancel);
                env.Store.Subscribe<CounterState>(Reference.Null, (_, _, _) => env.SecondFired++);
                return env;
            }
        }
    }
}

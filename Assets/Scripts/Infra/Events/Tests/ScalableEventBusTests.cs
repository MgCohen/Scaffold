using System;
using NUnit.Framework;

namespace Scaffold.Events.Tests
{
    public class ScalableEventBusTests
    {
        [Test]
        public void Raise_DerivedEvent_InvokesExactAndBaseGenericListeners()
        {
            ScalableEventBus bus = new ScalableEventBus();
            int baseCalls = 0;
            int derivedCalls = 0;
            bus.AddListener<BaseEvent>(_ => baseCalls++);
            bus.AddListener<DerivedEvent>(_ => derivedCalls++);
            bus.Raise(new DerivedEvent());
            Assert.AreEqual(1, baseCalls);
            Assert.AreEqual(1, derivedCalls);
        }

        [Test]
        public void Raise_DerivedEvent_InvokesBaseOpenTypeListener()
        {
            ScalableEventBus bus = new ScalableEventBus();
            int calls = 0;
            Action<ContextEvent> handler = _ => calls++;
            bus.AddListener(typeof(BaseEvent), handler);
            bus.Raise(new DerivedEvent());
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void AddListener_OpenTypeDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = new ScalableEventBus();
            int calls = 0;
            Action<ContextEvent> handler = _ => calls++;
            bus.AddListener(typeof(DerivedEvent), handler);
            bus.AddListener(typeof(DerivedEvent), handler);
            bus.Raise(new DerivedEvent());
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void RemoveListener_OpenTypeDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = new ScalableEventBus();
            int calls = 0;
            Action<ContextEvent> handler = _ => calls++;
            bus.AddListener(typeof(DerivedEvent), handler);
            bus.RemoveListener(typeof(DerivedEvent), handler);
            bus.RemoveListener(typeof(DerivedEvent), handler);
            bus.Raise(new DerivedEvent());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Raise_WithFailingListener_InvokesRemainingListeners()
        {
            ScalableEventBus bus = new ScalableEventBus();
            int successCalls = 0;
            bus.AddListener<DerivedEvent>(_ => throw new InvalidOperationException("failure"));
            bus.AddListener<DerivedEvent>(_ => successCalls++);
            Assert.DoesNotThrow(() => bus.Raise(new DerivedEvent()));
            Assert.AreEqual(1, successCalls);
        }

        [Test]
        public void AddListener_OpenTypeInvalidType_Throws()
        {
            ScalableEventBus bus = new ScalableEventBus();
            Action<ContextEvent> handler = _ => { };
            Assert.Throws<ArgumentException>(() => bus.AddListener(typeof(string), handler));
        }

        private abstract record BaseEvent : ContextEvent;
        private sealed record DerivedEvent : BaseEvent;
    }
}

using System;
using NUnit.Framework;

namespace Scaffold.Events.Tests
{
    public class ScalableEventBusTests
    {
        [Test]
        public void Raise_DerivedEvent_InvokesExactAndBaseGenericListeners()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int baseCalls = 0;
            int derivedCalls = 0;
            RegisterGenericHandlers(bus, () => baseCalls++, () => derivedCalls++);
            RaiseDerivedEvent(bus);
            Assert.AreEqual(1, baseCalls);
            Assert.AreEqual(1, derivedCalls);
        }

        [Test]
        public void Raise_DerivedEvent_InvokesBaseOpenTypeListener()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int calls = 0;
            Action<ContextEvent> handler = _ => calls++;
            bus.AddListener(typeof(BaseEvent), handler);
            DerivedEvent evt = new DerivedEvent();
            bus.Raise(evt);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void AddListener_OpenTypeDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int calls = 0;
            Action<ContextEvent> handler = _ => calls++;
            bus.AddListener(typeof(DerivedEvent), handler);
            bus.AddListener(typeof(DerivedEvent), handler);
            DerivedEvent evt = new DerivedEvent();
            bus.Raise(evt);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void RemoveListener_OpenTypeDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int calls = 0;
            Action<ContextEvent> handler = _ => calls++;
            AddAndRemoveOpenTypeHandler(bus, handler);
            RaiseDerivedEvent(bus);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void AddListener_GenericDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int calls = 0;
            Action<DerivedEvent> handler = _ => calls++;
            bus.AddListener(handler);
            bus.AddListener(handler);
            DerivedEvent evt = new DerivedEvent();
            bus.Raise(evt);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void RemoveListener_GenericDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int calls = 0;
            Action<DerivedEvent> handler = _ => calls++;
            AddAndRemoveGenericHandler(bus, handler);
            RaiseDerivedEvent(bus);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Raise_WithFailingListener_InvokesRemainingListeners()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int successCalls = 0;
            bus.AddListener<DerivedEvent>(_ => throw new InvalidOperationException("failure"));
            bus.AddListener<DerivedEvent>(_ => successCalls++);
            Assert.DoesNotThrow(() => RaiseDerivedEvent(bus));
            Assert.AreEqual(1, successCalls);
        }

        [Test]
        public void AddListener_OpenTypeInvalidType_Throws()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            Action<ContextEvent> handler = _ => { };
            Assert.Throws<ArgumentException>(() => bus.AddListener(typeof(string), handler));
        }

        private static void RegisterGenericHandlers(ScalableEventBus bus, Action onBase, Action onDerived)
        {
            bus.AddListener<BaseEvent>(_ => onBase());
            bus.AddListener<DerivedEvent>(_ => onDerived());
        }

        private static void AddAndRemoveOpenTypeHandler(ScalableEventBus bus, Action<ContextEvent> handler)
        {
            bus.AddListener(typeof(DerivedEvent), handler);
            bus.RemoveListener(typeof(DerivedEvent), handler);
            bus.RemoveListener(typeof(DerivedEvent), handler);
        }

        private static void AddAndRemoveGenericHandler(ScalableEventBus bus, Action<DerivedEvent> handler)
        {
            bus.AddListener(handler);
            bus.RemoveListener(handler);
            bus.RemoveListener(handler);
        }

        private static void RaiseDerivedEvent(ScalableEventBus bus)
        {
            DerivedEvent evt = new DerivedEvent();
            bus.Raise(evt);
        }

        private abstract record BaseEvent : ContextEvent;
        private sealed record DerivedEvent : BaseEvent;
    }

    internal static class ScalableEventBusTestFactory
    {
        public static ScalableEventBus Create(IEventMiddleware[] eventMiddlewares = null, IRequestMiddleware[] requestMiddlewares = null, IEventDiagnosticsSink diagnostics = null)
        {
            return new ScalableEventBus(eventMiddlewares, requestMiddlewares, diagnostics);
        }
    }
}

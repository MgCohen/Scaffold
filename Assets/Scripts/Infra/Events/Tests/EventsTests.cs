using System;
using NUnit.Framework;

namespace Scaffold.Events.Tests
{
    public class EventsTests
    {
        [Test]
        public void AddListener_WithSubscriber_CallsSubscriberOnRaise()
        {
            IEventBus bus = CreateBus();
            bool called = false;
            bus.AddListener<TestEvent>(_ => called = true);
            RaiseTestEvent(bus);
            Assert.IsTrue(called);
        }

        [Test]
        public void AddListener_OpenTypeWithSubscriber_CallsSubscriberOnRaise()
        {
            IEventBus bus = CreateBus();
            bool called = false;
            Action<ContextEvent> handler = _ => called = true;
            bus.AddListener(typeof(TestEvent), handler);
            RaiseTestEvent(bus);
            Assert.IsTrue(called);
        }

        [Test]
        public void RemoveListener_AfterSubscribing_StopsReceivingEvents()
        {
            IEventBus bus = CreateBus();
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;
            bus.AddListener(handler);
            bus.RemoveListener(handler);
            RaiseTestEvent(bus);
            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void RemoveListener_OpenTypeAfterSubscribing_StopsReceivingEvents()
        {
            IEventBus bus = CreateBus();
            int callCount = 0;
            Action<ContextEvent> handler = _ => callCount++;
            bus.AddListener(typeof(TestEvent), handler);
            bus.RemoveListener(typeof(TestEvent), handler);
            RaiseTestEvent(bus);
            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void AddListener_GenericDuplicate_IsIdempotent()
        {
            IEventBus bus = CreateBus();
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;
            bus.AddListener(handler);
            bus.AddListener(handler);
            RaiseTestEvent(bus);
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void RemoveListener_GenericDuplicate_IsIdempotent()
        {
            IEventBus bus = CreateBus();
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;
            bus.AddListener(handler);
            bus.RemoveListener(handler);
            bus.RemoveListener(handler);
            RaiseTestEvent(bus);
            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void SingleAddRaiseRemove_GenericFlow_CallsOnceAcrossLifecycle()
        {
            IEventBus bus = CreateBus();
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;
            bus.AddListener(handler);
            RaiseTestEvent(bus);
            bus.RemoveListener(handler);
            RaiseTestEvent(bus);
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void SingleAddRaiseRemove_OpenTypeFlow_CallsOnceAcrossLifecycle()
        {
            IEventBus bus = CreateBus();
            int callCount = 0;
            Action<ContextEvent> handler = _ => callCount++;
            bus.AddListener(typeof(TestEvent), handler);
            RaiseTestEvent(bus);
            bus.RemoveListener(typeof(TestEvent), handler);
            RaiseTestEvent(bus);
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void Clear_WithListeners_AllListenersAreRemoved()
        {
            IEventBus bus = CreateBus();
            bool called = false;
            bus.AddListener<TestEvent>(_ => called = true);
            bus.Clear();
            RaiseTestEvent(bus);
            Assert.IsFalse(called);
        }

        private static IEventBus CreateBus()
        {
            return ScalableEventBusTestFactory.Create();
        }

        private static void RaiseTestEvent(IEventBus bus)
        {
            TestEvent evt = new TestEvent();
            bus.Raise(evt);
        }

        private record TestEvent : ContextEvent;
    }
}

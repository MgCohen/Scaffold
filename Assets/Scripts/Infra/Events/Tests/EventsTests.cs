using System;
using NUnit.Framework;

namespace Scaffold.Events.Tests
{
    public class EventsTests
    {
        [Test]
        public void AddListener_WithSubscriber_CallsSubscriberOnRaise()
        {
            EventController bus = new EventController();
            bool called = false;
            bus.AddListener<TestEvent>(_ => called = true);
            RaiseTestEvent(bus);
            Assert.IsTrue(called);
        }

        [Test]
        public void AddListener_OpenTypeWithSubscriber_CallsSubscriberOnRaise()
        {
            EventController bus = new EventController();
            bool called = false;
            Action<ContextEvent> handler = _ => called = true;
            bus.AddListener(typeof(TestEvent), handler);
            RaiseTestEvent(bus);
            Assert.IsTrue(called);
        }

        [Test]
        public void RemoveListener_AfterSubscribing_StopsReceivingEvents()
        {
            EventController bus = new EventController();
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
            EventController bus = new EventController();
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
            EventController bus = new EventController();
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
            EventController bus = new EventController();
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
            EventController bus = new EventController();
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
            EventController bus = new EventController();
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
            EventController bus = new EventController();
            bool called = false;
            bus.AddListener<TestEvent>(_ => called = true);
            bus.Clear();
            RaiseTestEvent(bus);
            Assert.IsFalse(called);
        }

        private static void RaiseTestEvent(EventController bus)
        {
            TestEvent evt = new TestEvent();
            bus.Raise(evt);
        }

        private record TestEvent : ContextEvent;
    }
}

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
            var evt = new TestEvent();
            bus.Raise(evt);
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
            var evt = new TestEvent();
            bus.Raise(evt);
            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Clear_WithListeners_AllListenersAreRemoved()
        {
            EventController bus = new EventController();
            bool called = false;
            bus.AddListener<TestEvent>(_ => called = true);
            bus.Clear();
            var evt = new TestEvent();
            bus.Raise(evt);
            Assert.IsFalse(called);
        }

        private record TestEvent : ContextEvent;
    }
}

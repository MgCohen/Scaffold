using System.Collections.Generic;
using LiveOps.Signal;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class SignalModuleTests
    {
        [Fact]
        public void Unsubscribe_stops_further_pushes()
        {
            var bus = new SignalModule();
            int hits = 0;
            void Handler(int n) => hits += n;
            bus.Subscribe<int>(Handler);
            bus.Push(1);
            bus.Unsubscribe<int>(Handler);
            bus.Push(1);
            Assert.Equal(1, hits);
        }

        [Fact]
        public void Multi_subscriber_receives_in_subscription_order()
        {
            var bus = new SignalModule();
            var order = new List<int>();
            bus.Subscribe<int>(n => order.Add(1 * n));
            bus.Subscribe<int>(n => order.Add(2 * n));
            bus.Push(3);
            Assert.Equal(new[] { 3, 6 }, order);
        }
    }
}

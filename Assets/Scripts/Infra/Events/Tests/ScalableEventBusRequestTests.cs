using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Scaffold.Events.Tests
{
    public class ScalableEventBusRequestTests
    {
        [Test]
        public async Awaitable RequestAsync_WithGenericHandler_ReturnsExpectedResponse()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            bus.AddRequestHandler<PingRequest, int>(HandlePingAsync);
            PingRequest request = new PingRequest(7);
            int response = await bus.RequestAsync(request);
            Assert.AreEqual(7, response);
        }

        [Test]
        public async Awaitable RequestAsync_WithOpenTypeHandler_ReturnsExpectedResponse()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            Func<object, CancellationToken, Awaitable<object>> handler = (request, _) => HandleOpenTypeAsync((PingRequest)request);
            bus.AddRequestHandler(typeof(PingRequest), typeof(int), handler);
            PingRequest request = new PingRequest(11);
            int response = await bus.RequestAsync(request);
            Assert.AreEqual(11, response);
        }

        [Test]
        public void RequestAsync_NoHandler_ThrowsInvalidOperationException()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            PingRequest request = new PingRequest(5);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await bus.RequestAsync(request));
        }

        [Test]
        public void RequestAsync_HandlerThrows_ThrowsInvalidOperationException()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            bus.AddRequestHandler<PingRequest, int>(ThrowingHandlerAsync);
            PingRequest request = new PingRequest(2);
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await bus.RequestAsync(request));
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        [Test]
        public void RequestAsync_WithCanceledToken_ThrowsOperationCanceledExceptionWithoutInvokingHandler()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int calls = 0;
            bus.AddRequestHandler<PingRequest, int>((request, token) => CountingHandlerAsync(request, token, () => calls++));
            CancellationToken cancellationToken = new CancellationToken(canceled: true);
            PingRequest request = new PingRequest(3);
            Assert.ThrowsAsync<OperationCanceledException>(async () => await bus.RequestAsync(request, cancellationToken));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public async Awaitable AddRequestHandler_GenericDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int calls = 0;
            Func<PingRequest, CancellationToken, Awaitable<int>> handler = (request, token) => CountingReturnHandlerAsync(request, token, () => calls++);
            AddGenericHandlerTwice(bus, handler);
            PingRequest request = CreateRequest(9);
            int response = await bus.RequestAsync(request);
            Assert.AreEqual(9, response);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void RemoveRequestHandler_GenericDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            Func<PingRequest, CancellationToken, Awaitable<int>> handler = HandlePingAsync;
            bus.AddRequestHandler(handler);
            bus.RemoveRequestHandler(handler);
            bus.RemoveRequestHandler(handler);
            PingRequest request = new PingRequest(1);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await bus.RequestAsync(request));
        }

        [Test]
        public async Awaitable AddRequestHandler_OpenTypeDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            int calls = 0;
            Func<object, CancellationToken, Awaitable<object>> handler = (request, token) => CountingOpenTypeHandlerAsync((PingRequest)request, token, () => calls++);
            AddOpenTypeHandlerTwice(bus, handler);
            PingRequest request = CreateRequest(13);
            int response = await bus.RequestAsync(request);
            Assert.AreEqual(13, response);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void RemoveRequestHandler_OpenTypeDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            Func<object, CancellationToken, Awaitable<object>> handler = (request, _) => HandleOpenTypeAsync((PingRequest)request);
            bus.AddRequestHandler(typeof(PingRequest), typeof(int), handler);
            bus.RemoveRequestHandler(typeof(PingRequest), typeof(int), handler);
            bus.RemoveRequestHandler(typeof(PingRequest), typeof(int), handler);
            PingRequest request = CreateRequest(4);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await bus.RequestAsync(request));
        }

        [Test]
        public void AddRequestHandler_OpenTypeMismatchedResponseType_ThrowsArgumentException()
        {
            ScalableEventBus bus = ScalableEventBusTestFactory.Create();
            Func<object, CancellationToken, Awaitable<object>> handler = (request, _) => HandleOpenTypeAsync((PingRequest)request);
            Assert.Throws<ArgumentException>(() => bus.AddRequestHandler(typeof(PingRequest), typeof(string), handler));
        }

        private static void AddGenericHandlerTwice(ScalableEventBus bus, Func<PingRequest, CancellationToken, Awaitable<int>> handler)
        {
            bus.AddRequestHandler(handler);
            bus.AddRequestHandler(handler);
        }

        private static void AddOpenTypeHandlerTwice(ScalableEventBus bus, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            bus.AddRequestHandler(typeof(PingRequest), typeof(int), handler);
            bus.AddRequestHandler(typeof(PingRequest), typeof(int), handler);
        }

        private static PingRequest CreateRequest(int value)
        {
            return new PingRequest(value);
        }

        private static async Awaitable<int> HandlePingAsync(PingRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return request.Value;
        }

        private static async Awaitable<object> HandleOpenTypeAsync(PingRequest request)
        {
            return request.Value;
        }

        private static async Awaitable<int> ThrowingHandlerAsync(PingRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("request-failure");
        }

        private static async Awaitable<int> CountingHandlerAsync(PingRequest request, CancellationToken cancellationToken, Action onCall)
        {
            onCall();
            cancellationToken.ThrowIfCancellationRequested();
            return request.Value;
        }

        private static async Awaitable<int> CountingReturnHandlerAsync(PingRequest request, CancellationToken cancellationToken, Action onCall)
        {
            onCall();
            cancellationToken.ThrowIfCancellationRequested();
            return request.Value;
        }

        private static async Awaitable<object> CountingOpenTypeHandlerAsync(PingRequest request, CancellationToken cancellationToken, Action onCall)
        {
            onCall();
            cancellationToken.ThrowIfCancellationRequested();
            return request.Value;
        }

        private sealed record PingRequest : ContextRequest<int>
        {
            public PingRequest(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}

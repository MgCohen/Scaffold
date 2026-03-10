using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Scaffold.Events.Tests
{
    public class ScalableEventBusRequestTests
    {
        [Test]
        public async Task RequestAsync_WithGenericHandler_ReturnsExpectedResponse()
        {
            ScalableEventBus bus = new ScalableEventBus();
            bus.AddRequestHandler<PingRequest, int>(HandlePingAsync);
            int response = await bus.RequestAsync(new PingRequest(7));
            Assert.AreEqual(7, response);
        }

        [Test]
        public async Task RequestAsync_WithOpenTypeHandler_ReturnsExpectedResponse()
        {
            ScalableEventBus bus = new ScalableEventBus();
            Func<object, CancellationToken, Awaitable<object>> handler = (request, _) => HandleOpenTypeAsync((PingRequest)request);
            bus.AddRequestHandler(typeof(PingRequest), typeof(int), handler);
            int response = await bus.RequestAsync(new PingRequest(11));
            Assert.AreEqual(11, response);
        }

        [Test]
        public void RequestAsync_NoHandler_ThrowsInvalidOperationException()
        {
            ScalableEventBus bus = new ScalableEventBus();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await bus.RequestAsync(new PingRequest(5)));
        }

        [Test]
        public void RequestAsync_HandlerThrows_ThrowsInvalidOperationException()
        {
            ScalableEventBus bus = new ScalableEventBus();
            bus.AddRequestHandler<PingRequest, int>(ThrowingHandlerAsync);
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await bus.RequestAsync(new PingRequest(2)));
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        [Test]
        public void RequestAsync_WithCanceledToken_ThrowsOperationCanceledExceptionWithoutInvokingHandler()
        {
            ScalableEventBus bus = new ScalableEventBus();
            int calls = 0;
            bus.AddRequestHandler<PingRequest, int>((request, token) => CountingHandlerAsync(request, token, () => calls++));
            CancellationToken cancellationToken = new CancellationToken(canceled: true);
            Assert.ThrowsAsync<OperationCanceledException>(async () => await bus.RequestAsync(new PingRequest(3), cancellationToken));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public async Task AddRequestHandler_GenericDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = new ScalableEventBus();
            int calls = 0;
            Func<PingRequest, CancellationToken, Awaitable<int>> handler = (request, token) => CountingReturnHandlerAsync(request, token, () => calls++);
            bus.AddRequestHandler(handler);
            bus.AddRequestHandler(handler);
            int response = await bus.RequestAsync(new PingRequest(9));
            Assert.AreEqual(9, response);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void RemoveRequestHandler_GenericDuplicate_IsIdempotent()
        {
            ScalableEventBus bus = new ScalableEventBus();
            Func<PingRequest, CancellationToken, Awaitable<int>> handler = HandlePingAsync;
            bus.AddRequestHandler(handler);
            bus.RemoveRequestHandler(handler);
            bus.RemoveRequestHandler(handler);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await bus.RequestAsync(new PingRequest(1)));
        }

        [Test]
        public void AddRequestHandler_OpenTypeMismatchedResponseType_ThrowsArgumentException()
        {
            ScalableEventBus bus = new ScalableEventBus();
            Func<object, CancellationToken, Awaitable<object>> handler = (request, _) => HandleOpenTypeAsync((PingRequest)request);
            Assert.Throws<ArgumentException>(() => bus.AddRequestHandler(typeof(PingRequest), typeof(string), handler));
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

using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Scaffold.Events.Tests
{
    public class ScalableEventBusMiddlewareDiagnosticsTests
    {
        [Test]
        public void Raise_EventMiddlewareOrder_IsDeterministic()
        {
            List<string> trace = CreateTrace();
            IEventMiddleware[] middleware = CreateEventMiddlewares(trace);
            ScalableEventBus bus = CreateBus(middleware, null, null);
            RegisterEventHandler(trace, bus);
            RaiseMiddlewareEvent(bus);
            string[] expected = CreateExpectedOrder();
            CollectionAssert.AreEqual(expected, trace);
        }

        [Test]
        public async Awaitable RequestAsync_RequestMiddlewareOrder_IsDeterministic()
        {
            List<string> trace = CreateTrace();
            int response = await ExecuteRequestWithTrace(trace, 3);
            Assert.AreEqual(3, response);
            string[] expected = CreateExpectedOrder();
            CollectionAssert.AreEqual(expected, trace);
        }

        [Test]
        public void Raise_WithThrowingListener_InvokesDiagnosticsHooks()
        {
            CountingDiagnosticsSink diagnostics = new CountingDiagnosticsSink();
            ScalableEventBus bus = CreateBus(null, null, diagnostics);
            RegisterThrowingListeners(bus);
            RaiseMiddlewareEvent(bus);
            AssertListenerFailureDiagnostics(diagnostics);
        }

        [Test]
        public async Awaitable RequestAsync_InvokesRequestCompletionDiagnosticsForSuccessAndFailure()
        {
            CountingDiagnosticsSink diagnostics = new CountingDiagnosticsSink();
            ScalableEventBus bus = CreateBusForSuccessfulRequest(diagnostics);
            int success = await RequestValue(bus, 10);
            Assert.AreEqual(10, success);
            bus.Clear();
            bus.AddRequestHandler<MiddlewareRequest, int>((_, _) => ThrowFailureAsync());
            AssertThrowsInvalidOperationOnRequest(bus);
            AssertRequestCompletionDiagnostics(diagnostics);
        }

        private static void AssertThrowsInvalidOperationOnRequest(ScalableEventBus bus)
        {
            MiddlewareRequest failureRequest = CreateRequest(20);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await bus.RequestAsync(failureRequest));
        }

        private static ScalableEventBus CreateBusForSuccessfulRequest(CountingDiagnosticsSink diagnostics)
        {
            ScalableEventBus bus = CreateBus(null, null, diagnostics);
            bus.AddRequestHandler<MiddlewareRequest, int>((request, _) => ReturnValueAsync(request.Value));
            return bus;
        }

        private static async Awaitable<int> RequestValue(ScalableEventBus bus, int value)
        {
            MiddlewareRequest request = CreateRequest(value);
            int response = await bus.RequestAsync(request);
            return response;
        }

        private static async Awaitable<int> ExecuteRequestWithTrace(List<string> trace, int value)
        {
            IRequestMiddleware[] middleware = CreateRequestMiddlewares(trace);
            ScalableEventBus bus = CreateBus(null, middleware, null);
            bus.AddRequestHandler<MiddlewareRequest, int>((request, _) => ReturnValueAsync(request.Value));
            int response = await RequestValue(bus, value);
            return response;
        }

        private static void RegisterThrowingListeners(ScalableEventBus bus)
        {
            bus.AddListener<MiddlewareEvent>(_ => { });
            bus.AddListener<MiddlewareEvent>(_ => throw new InvalidOperationException("listener-failure"));
        }

        private static void AssertListenerFailureDiagnostics(CountingDiagnosticsSink diagnostics)
        {
            Assert.AreEqual(1, diagnostics.PublishedCount);
            Assert.AreEqual(2, diagnostics.ListenerInvokedCount);
            Assert.AreEqual(1, diagnostics.ListenerFailedCount);
            Assert.AreEqual(0, diagnostics.RequestCompletedCount);
        }

        private static void AssertRequestCompletionDiagnostics(CountingDiagnosticsSink diagnostics)
        {
            Assert.AreEqual(2, diagnostics.RequestCompletedCount);
            Assert.AreEqual(1, diagnostics.RequestSuccessCount);
            Assert.AreEqual(1, diagnostics.RequestFailureCount);
        }

        private static async Awaitable<int> ReturnValueAsync(int value)
        {
            return value;
        }

        private static async Awaitable<int> ThrowFailureAsync()
        {
            throw new InvalidOperationException("request-failure");
        }

        private static string[] CreateExpectedOrder()
        {
            return new[] { "before-A", "before-B", "handler", "after-B", "after-A" };
        }

        private static List<string> CreateTrace()
        {
            return new List<string>();
        }

        private static ScalableEventBus CreateBus(IEventMiddleware[] eventMiddlewares, IRequestMiddleware[] requestMiddlewares, IEventDiagnosticsSink diagnostics)
        {
            return ScalableEventBusTestFactory.Create(eventMiddlewares, requestMiddlewares, diagnostics);
        }

        private static MiddlewareRequest CreateRequest(int value)
        {
            return new MiddlewareRequest(value);
        }

        private static void RegisterEventHandler(List<string> trace, ScalableEventBus bus)
        {
            bus.AddListener<MiddlewareEvent>(_ => trace.Add("handler"));
        }

        private static void RaiseMiddlewareEvent(ScalableEventBus bus)
        {
            MiddlewareEvent evt = new MiddlewareEvent();
            bus.Raise(evt);
        }

        private static IEventMiddleware[] CreateEventMiddlewares(List<string> trace)
        {
            TraceEventMiddleware middlewareA = new TraceEventMiddleware("A", trace);
            TraceEventMiddleware middlewareB = new TraceEventMiddleware("B", trace);
            return new IEventMiddleware[] { middlewareA, middlewareB };
        }

        private static IRequestMiddleware[] CreateRequestMiddlewares(List<string> trace)
        {
            TraceRequestMiddleware middlewareA = new TraceRequestMiddleware("A", trace);
            TraceRequestMiddleware middlewareB = new TraceRequestMiddleware("B", trace);
            return new IRequestMiddleware[] { middlewareA, middlewareB };
        }

        private sealed class TraceEventMiddleware : IEventMiddleware
        {
            private readonly string name;
            private readonly List<string> trace;

            public TraceEventMiddleware(string name, List<string> trace)
            {
                this.name = name;
                this.trace = trace;
            }

            public void Invoke(ContextEvent evt, Action next)
            {
                trace.Add("before-" + name);
                next();
                trace.Add("after-" + name);
            }
        }

        private sealed class TraceRequestMiddleware : IRequestMiddleware
        {
            private readonly string name;
            private readonly List<string> trace;

            public TraceRequestMiddleware(string name, List<string> trace)
            {
                this.name = name;
                this.trace = trace;
            }

            public async Awaitable<TResponse> Invoke<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, Func<ContextRequest<TResponse>, CancellationToken, Awaitable<TResponse>> next)
            {
                trace.Add("before-" + name);
                TResponse response = await next(request, cancellationToken);
                trace.Add("after-" + name);
                return response;
            }
        }

        private sealed class CountingDiagnosticsSink : IEventDiagnosticsSink
        {
            public int PublishedCount { get; private set; }
            public int ListenerInvokedCount { get; private set; }
            public int ListenerFailedCount { get; private set; }
            public int RequestCompletedCount { get; private set; }
            public int RequestSuccessCount { get; private set; }
            public int RequestFailureCount { get; private set; }

            public void OnEventPublished(EventDispatchContext context, int listenerCount)
            {
                PublishedCount++;
            }

            public void OnListenerInvoked(EventDispatchContext context, Type declaredType, double durationMs)
            {
                ListenerInvokedCount++;
            }

            public void OnListenerFailed(EventDispatchContext context, Exception exception)
            {
                ListenerFailedCount++;
            }

            public void OnRequestCompleted(EventDispatchContext context, bool success, double durationMs)
            {
                RequestCompletedCount++;
                if (success) { RequestSuccessCount++; }
                if (!success) { RequestFailureCount++; }
            }
        }

        private sealed record MiddlewareEvent : ContextEvent;

        private sealed record MiddlewareRequest : ContextRequest<int>
        {
            public MiddlewareRequest(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}

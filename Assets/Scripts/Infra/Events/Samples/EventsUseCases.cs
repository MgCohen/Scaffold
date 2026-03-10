using System;
using System.Threading;
using UnityEngine;

namespace Scaffold.Events.Samples
{
    public class EventsUseCases
    {
        public void UseCaseSubscribeAndPublishEvent()
        {
            IEventBus bus = CreateBus();
            bool received = false;
            bus.AddListener<PlayerDiedEvent>(_ => received = true);
            var evt = new PlayerDiedEvent();
            bus.Raise(evt);
            Debug.Log("Received: " + received);
        }

        public void UseCaseUnsubscribeFromEvent()
        {
            IEventBus bus = CreateBus();
            int count = 0;
            Action<PlayerDiedEvent> handler = _ => count++;
            bus.AddListener(handler);
            bus.RemoveListener(handler);
            var evt = new PlayerDiedEvent();
            bus.Raise(evt);
        }

        public void UseCaseSubscribeAndUnsubscribeWithOpenTypeApi()
        {
            IEventBus bus = CreateBus();
            int count = RunOpenTypeLifecycle(bus);
            Debug.Log("OpenTypeCount: " + count);
        }

        public async Awaitable UseCaseRequestResponse()
        {
            IRequestBus bus = CreateBus();
            bus.AddRequestHandler<LoadScoreRequest, int>(HandleLoadScoreAsync);
            LoadScoreRequest request = new LoadScoreRequest(42);
            int score = await bus.RequestAsync(request);
            Debug.Log("LoadedScore: " + score);
        }

#pragma warning disable CS0618
        public void UseCaseLegacyEventControllerCompatibility()
        {
            EventController bus = new EventController();
            bool received = false;
            bus.AddListener<PlayerDiedEvent>(_ => received = true);
            RaisePlayerDiedEvent(bus);
            Debug.Log("LegacyReceived: " + received);
        }
#pragma warning restore CS0618

        private static int RunOpenTypeLifecycle(IEventBus bus)
        {
            int count = 0;
            Action<ContextEvent> handler = _ => count++;
            Type eventType = typeof(PlayerDiedEvent);
            bus.AddListener(eventType, handler);
            RaisePlayerDiedEvent(bus);
            bus.RemoveListener(eventType, handler);
            RaisePlayerDiedEvent(bus);
            return count;
        }

        private static void RaisePlayerDiedEvent(IEventBus bus)
        {
            PlayerDiedEvent evt = new PlayerDiedEvent();
            bus.Raise(evt);
        }

#pragma warning disable CS1998
        private static async Awaitable<int> HandleLoadScoreAsync(LoadScoreRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return request.Score;
        }
#pragma warning restore CS1998

        private static ScalableEventBus CreateBus()
        {
            IEventMiddleware[] eventMiddlewares = Array.Empty<IEventMiddleware>();
            IRequestMiddleware[] requestMiddlewares = Array.Empty<IRequestMiddleware>();
            IEventDiagnosticsSink diagnosticsSink = NoOpEventDiagnosticsSink.Instance;
            ScalableEventBus bus = new ScalableEventBus(eventMiddlewares, requestMiddlewares, diagnosticsSink);
            return bus;
        }

        private record PlayerDiedEvent : ContextEvent;

        private sealed record LoadScoreRequest : ContextRequest<int>
        {
            public LoadScoreRequest(int score)
            {
                Score = score;
            }

            public int Score { get; }
        }
    }
}

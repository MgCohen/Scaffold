using System;

namespace Scaffold.Events
{
    public sealed class NoOpEventDiagnosticsSink : IEventDiagnosticsSink
    {
        public static readonly NoOpEventDiagnosticsSink Instance = new NoOpEventDiagnosticsSink();

        private NoOpEventDiagnosticsSink()
        {
        }

        public void OnEventPublished(EventDispatchContext context, int listenerCount)
        {
        }

        public void OnListenerInvoked(EventDispatchContext context, Type declaredType, double durationMs)
        {
        }

        public void OnListenerFailed(EventDispatchContext context, Exception exception)
        {
        }

        public void OnRequestCompleted(EventDispatchContext context, bool success, double durationMs)
        {
        }
    }
}

using System;

namespace Scaffold.Events
{
    public interface IEventDiagnosticsSink
    {
        void OnEventPublished(EventDispatchContext context, int listenerCount);
        void OnListenerInvoked(EventDispatchContext context, Type declaredType, double durationMs);
        void OnListenerFailed(EventDispatchContext context, Exception exception);
        void OnRequestCompleted(EventDispatchContext context, bool success, double durationMs);
    }
}

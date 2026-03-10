using System;

namespace Scaffold.Events
{
    public readonly struct EventDispatchContext
    {
        public EventDispatchContext(Guid correlationId, DateTimeOffset timestampUtc, Type messageType, bool isRequest)
        {
            CorrelationId = correlationId;
            TimestampUtc = timestampUtc;
            MessageType = messageType;
            IsRequest = isRequest;
        }

        public Guid CorrelationId { get; }
        public DateTimeOffset TimestampUtc { get; }
        public Type MessageType { get; }
        public bool IsRequest { get; }

        public static EventDispatchContext ForEvent(Type eventType)
        {
            Guid correlationId = Guid.NewGuid();
            DateTimeOffset timestampUtc = DateTimeOffset.UtcNow;
            return new EventDispatchContext(correlationId, timestampUtc, eventType, false);
        }

        public static EventDispatchContext ForRequest(Type requestType)
        {
            Guid correlationId = Guid.NewGuid();
            DateTimeOffset timestampUtc = DateTimeOffset.UtcNow;
            return new EventDispatchContext(correlationId, timestampUtc, requestType, true);
        }
    }
}

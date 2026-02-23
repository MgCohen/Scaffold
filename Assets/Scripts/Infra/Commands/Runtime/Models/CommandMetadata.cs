using System;

namespace Scaffold.Commands
{
    /// <summary>
    /// Public delivery metadata for command listeners.
    /// Source identity is intentionally omitted because queue partitioning is internal behavior.
    /// </summary>
    public class CommandMetadata
    {
        public CommandMetadata(string messageId, long sequence, DateTime createdAtUtc, DateTime receivedAtUtc, string correlationId)
        {
            MessageId = messageId;
            Sequence = sequence;
            CreatedAtUtc = createdAtUtc;
            ReceivedAtUtc = receivedAtUtc;
            CorrelationId = correlationId;
        }

        public string MessageId { get; }

        public long Sequence { get; }

        public DateTime CreatedAtUtc { get; }

        public DateTime ReceivedAtUtc { get; }

        public string CorrelationId { get; }
    }
}

using System;

namespace Scaffold.Commands
{
    /// <summary>
    /// Public delivery metadata for command listeners.
    /// </summary>
    public class CommandMetadata
    {
        public CommandMetadata(string messageId, DateTime createdAtUtc, DateTime receivedAtUtc, string correlationId)
        {
            MessageId = messageId;
            CreatedAtUtc = createdAtUtc;
            ReceivedAtUtc = receivedAtUtc;
            CorrelationId = correlationId;
        }

        public string MessageId { get; }

        public DateTime CreatedAtUtc { get; }

        public DateTime ReceivedAtUtc { get; }

        public string CorrelationId { get; }
    }
}

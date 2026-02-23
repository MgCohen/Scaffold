using System;

namespace Scaffold.Commands
{
    /// <summary>
    /// Low-level transport shape used between queue and transport integrations.
    /// Source id and source type are used by the internal queue stream key.
    /// </summary>
    public class CommandTransportMessage
    {
        public CommandTransportMessage(ICommand message, CommandSourceType sourceType, ulong sourceId, long sequence, string messageId, DateTime createdAtUtc, string correlationId)
        {
            var hasMessage = message != null;
            if (!hasMessage)
            {
                throw new ArgumentNullException(nameof(message));
            }
            var hasMessageId = !string.IsNullOrWhiteSpace(messageId);
            if (!hasMessageId)
            {
                throw new ArgumentException("Message id is required.", nameof(messageId));
            }
            Message = message;
            SourceType = sourceType;
            SourceId = sourceId;
            Sequence = sequence;
            MessageId = messageId;
            CreatedAtUtc = createdAtUtc;
            CorrelationId = correlationId;
        }

        public ICommand Message { get; }

        public CommandSourceType SourceType { get; }

        public ulong SourceId { get; }

        public long Sequence { get; }

        public string MessageId { get; }

        public DateTime CreatedAtUtc { get; }

        public string CorrelationId { get; }
    }
}

using System;

namespace Scaffold.Commands
{
    /// <summary>
    /// Low-level transport shape used by command transport adapters.
    /// </summary>
    public class CommandTransportMessage
    {
        public CommandTransportMessage(ICommand message, string messageId, DateTime createdAtUtc, string correlationId)
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
            MessageId = messageId;
            CreatedAtUtc = createdAtUtc;
            CorrelationId = correlationId;
        }

        public ICommand Message { get; }

        public string MessageId { get; }

        public DateTime CreatedAtUtc { get; }

        public string CorrelationId { get; }
    }
}

using System;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Metadata used for ordering, tracing, and source-aware dispatch.
    /// </summary>
    public class CommandMetadata
    {
        public CommandMetadata(string messageId, CommandSource source, long sequence, DateTime createdAtUtc, DateTime receivedAtUtc, string correlationId)
        {
            MessageId = messageId;
            Source = source;
            Sequence = sequence;
            CreatedAtUtc = createdAtUtc;
            ReceivedAtUtc = receivedAtUtc;
            CorrelationId = correlationId;
        }

        public string MessageId { get; }

        public CommandSource Source { get; }

        public long Sequence { get; }

        public DateTime CreatedAtUtc { get; }

        public DateTime ReceivedAtUtc { get; }

        public string CorrelationId { get; }

        public CommandMetadata WithReceivedAtUtc(DateTime receivedAtUtc)
        {
            var metadata = new CommandMetadata(MessageId, Source, Sequence, CreatedAtUtc, receivedAtUtc, CorrelationId);
            return metadata;
        }
    }
}

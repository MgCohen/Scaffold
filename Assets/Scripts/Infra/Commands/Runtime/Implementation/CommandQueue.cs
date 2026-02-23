using System;
using System.Collections.Generic;

namespace Scaffold.Commands
{
    /// <summary>
    /// Ordered queue that buffers out-of-order messages per sender stream.
    /// </summary>
    internal class CommandQueue
    {
        private readonly Dictionary<QueueStreamKey, SourceQueueState> sourceStates = new Dictionary<QueueStreamKey, SourceQueueState>();
        private readonly long firstIncomingSequence;
        private readonly bool bootstrapFromFirstMessage;

        public CommandQueue(long firstIncomingSequenceValue, bool bootstrapIncomingFromFirstMessage)
        {
            firstIncomingSequence = firstIncomingSequenceValue;
            bootstrapFromFirstMessage = bootstrapIncomingFromFirstMessage;
        }

        public List<CommandDispatchItem> Enqueue(CommandTransportMessage message)
        {
            var readyItems = new List<CommandDispatchItem>();
            var queuedItem = CreateDispatchItem(message);
            var streamKey = CreateStreamKey(message);
            var state = GetOrCreateState(streamKey, message.Sequence);
            ProcessSequence(state, message.Sequence, queuedItem, readyItems);
            return readyItems;
        }

        private CommandDispatchItem CreateDispatchItem(CommandTransportMessage message)
        {
            var receivedAtUtc = DateTime.UtcNow;
            var metadata = new CommandMetadata(message.MessageId, message.Sequence, message.CreatedAtUtc, receivedAtUtc, message.CorrelationId);
            var dispatchItem = new CommandDispatchItem(message.Message, metadata);
            return dispatchItem;
        }

        private QueueStreamKey CreateStreamKey(CommandTransportMessage message)
        {
            var streamKey = new QueueStreamKey(message.SourceType, message.SourceId);
            return streamKey;
        }

        private SourceQueueState GetOrCreateState(QueueStreamKey streamKey, long sequence)
        {
            var hasState = sourceStates.TryGetValue(streamKey, out var state);
            if (!hasState)
            {
                var expectedSequence = ResolveExpectedSequence(sequence);
                state = new SourceQueueState(expectedSequence);
                sourceStates.Add(streamKey, state);
            }
            return state;
        }

        private long ResolveExpectedSequence(long firstSeenSequence)
        {
            var useFirstSeenSequence = bootstrapFromFirstMessage;
            if (useFirstSeenSequence)
            {
                return firstSeenSequence;
            }
            return firstIncomingSequence;
        }

        private void ProcessSequence(SourceQueueState state, long sequence, CommandDispatchItem dispatchItem, List<CommandDispatchItem> readyItems)
        {
            var isStaleSequence = sequence < state.ExpectedSequence;
            var isFutureSequence = sequence > state.ExpectedSequence;
            if (isStaleSequence)
            {
                return;
            }
            if (isFutureSequence)
            {
                AddPendingItem(state, sequence, dispatchItem);
                return;
            }
            AddReadyItem(state, dispatchItem, readyItems);
            FlushPendingItems(state, readyItems);
        }

        private void AddPendingItem(SourceQueueState state, long sequence, CommandDispatchItem dispatchItem)
        {
            var hasSequence = state.PendingItems.ContainsKey(sequence);
            if (!hasSequence)
            {
                state.PendingItems.Add(sequence, dispatchItem);
            }
        }

        private void AddReadyItem(SourceQueueState state, CommandDispatchItem dispatchItem, List<CommandDispatchItem> readyItems)
        {
            readyItems.Add(dispatchItem);
            state.ExpectedSequence = state.ExpectedSequence + 1;
        }

        private void FlushPendingItems(SourceQueueState state, List<CommandDispatchItem> readyItems)
        {
            var hasPendingNext = state.PendingItems.TryGetValue(state.ExpectedSequence, out var pendingItem);
            while (hasPendingNext)
            {
                state.PendingItems.Remove(state.ExpectedSequence);
                readyItems.Add(pendingItem);
                state.ExpectedSequence = state.ExpectedSequence + 1;
                hasPendingNext = state.PendingItems.TryGetValue(state.ExpectedSequence, out pendingItem);
            }
        }

        private readonly struct QueueStreamKey : IEquatable<QueueStreamKey>
        {
            public QueueStreamKey(CommandSourceType sourceType, ulong sourceId)
            {
                SourceType = sourceType;
                SourceId = sourceId;
            }

            public CommandSourceType SourceType { get; }

            public ulong SourceId { get; }

            public bool Equals(QueueStreamKey other)
            {
                return SourceType == other.SourceType && SourceId == other.SourceId;
            }

            public override bool Equals(object obj)
            {
                var hasValue = obj is QueueStreamKey key;
                if (hasValue)
                {
                    return Equals(key);
                }
                return false;
            }

            public override int GetHashCode()
            {
                var hash = (int)SourceType;
                hash = (hash * 397) ^ SourceId.GetHashCode();
                return hash;
            }
        }

        private sealed class SourceQueueState
        {
            public SourceQueueState(long expectedSequence)
            {
                ExpectedSequence = expectedSequence;
                PendingItems = new SortedDictionary<long, CommandDispatchItem>();
            }

            public long ExpectedSequence { get; set; }

            public SortedDictionary<long, CommandDispatchItem> PendingItems { get; }
        }
    }
}

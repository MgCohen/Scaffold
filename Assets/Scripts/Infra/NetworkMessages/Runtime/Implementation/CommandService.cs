using System;
using System.Collections.Generic;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Instance-based command service with sender-keyed sequence ordering and typed subscriptions.
    /// </summary>
    public class CommandService : ICommandService, IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly ICommandTransport transport;
        private readonly CommandServiceOptions options;
        private readonly Dictionary<Type, List<SubscriptionEntry>> subscribersByType = new Dictionary<Type, List<SubscriptionEntry>>();
        private readonly Dictionary<CommandSource, SourceQueueState> sourceStates = new Dictionary<CommandSource, SourceQueueState>();
        private long nextOutgoingSequence;
        private bool isDisposed;

        public CommandService(ICommandTransport commandTransport, CommandServiceOptions commandOptions = null)
        {
            var hasTransport = commandTransport != null;
            if (!hasTransport)
            {
                throw new ArgumentNullException(nameof(commandTransport));
            }
            transport = commandTransport;
            options = ResolveOptions(commandOptions);
            nextOutgoingSequence = options.FirstOutgoingSequence;
            transport.AddReceiver(HandleIncomingEnvelope);
        }

        public void Send<TCommand>(TCommand command) where TCommand : ICommand
        {
            EnsureNotDisposed();
            ICommand commandPayload = command;
            var envelope = CreateOutgoingEnvelope(commandPayload);
            transport.Send(envelope);
        }

        public IDisposable Subscribe<TCommand>(Action<TCommand, CommandMetadata> handler) where TCommand : ICommand
        {
            var subscription = Subscribe(handler, null);
            return subscription;
        }

        public IDisposable Subscribe<TCommand>(Action<TCommand, CommandMetadata> handler, Predicate<CommandMetadata> metadataFilter) where TCommand : ICommand
        {
            EnsureNotDisposed();
            var hasHandler = handler != null;
            if (!hasHandler)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            var commandType = typeof(TCommand);
            var entry = CreateSubscriptionEntry(handler, metadataFilter);
            AddSubscription(commandType, entry);
            var subscription = new CommandSubscription(() => Unsubscribe(commandType, entry));
            return subscription;
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                transport.RemoveReceiver(HandleIncomingEnvelope);
                lock (syncRoot)
                {
                    subscribersByType.Clear();
                    sourceStates.Clear();
                }
            }
        }

        private void HandleIncomingEnvelope(CommandEnvelope envelope)
        {
            var disposed = isDisposed;
            if (!disposed)
            {
                var hasEnvelope = envelope != null;
                if (hasEnvelope)
                {
                    var normalizedEnvelope = NormalizeIncomingEnvelope(envelope);
                    List<CommandEnvelope> readyEnvelopes;
                    lock (syncRoot)
                    {
                        readyEnvelopes = CollectReadyEnvelopes(normalizedEnvelope);
                    }
                    DispatchEnvelopes(readyEnvelopes);
                }
            }
        }

        private CommandServiceOptions ResolveOptions(CommandServiceOptions commandOptions)
        {
            var hasOptions = commandOptions != null;
            if (hasOptions)
            {
                return commandOptions;
            }
            var defaultOptions = CommandServiceOptions.CreateDefault();
            return defaultOptions;
        }

        private void EnsureNotDisposed()
        {
            var disposed = isDisposed;
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(CommandService));
            }
        }

        private CommandEnvelope CreateOutgoingEnvelope(ICommand command)
        {
            var metadata = CreateOutgoingMetadata();
            var envelope = new CommandEnvelope(command, metadata);
            return envelope;
        }

        private CommandMetadata CreateOutgoingMetadata()
        {
            long sequence;
            lock (syncRoot)
            {
                sequence = nextOutgoingSequence;
                nextOutgoingSequence = nextOutgoingSequence + 1;
            }
            var guid = Guid.NewGuid();
            var messageId = guid.ToString("N");
            var createdAtUtc = DateTime.UtcNow;
            var metadata = new CommandMetadata(messageId, options.LocalSource, sequence, createdAtUtc, createdAtUtc, string.Empty);
            return metadata;
        }

        private SubscriptionEntry CreateSubscriptionEntry<TCommand>(Action<TCommand, CommandMetadata> handler, Predicate<CommandMetadata> metadataFilter) where TCommand : ICommand
        {
            Action<ICommand, CommandMetadata> callback = (command, metadata) => handler((TCommand)command, metadata);
            var entry = new SubscriptionEntry(callback, metadataFilter);
            return entry;
        }

        private void AddSubscription(Type commandType, SubscriptionEntry entry)
        {
            lock (syncRoot)
            {
                var hasSubscribers = subscribersByType.TryGetValue(commandType, out var entries);
                if (!hasSubscribers)
                {
                    entries = new List<SubscriptionEntry>();
                    subscribersByType.Add(commandType, entries);
                }
                entries.Add(entry);
            }
        }

        private void Unsubscribe(Type commandType, SubscriptionEntry entry)
        {
            var disposed = isDisposed;
            if (!disposed)
            {
                lock (syncRoot)
                {
                    var hasSubscribers = subscribersByType.TryGetValue(commandType, out var entries);
                    if (hasSubscribers)
                    {
                        entries.Remove(entry);
                        var isEmpty = entries.Count == 0;
                        if (isEmpty)
                        {
                            subscribersByType.Remove(commandType);
                        }
                    }
                }
            }
        }

        private CommandEnvelope NormalizeIncomingEnvelope(CommandEnvelope envelope)
        {
            var receivedAtUtc = DateTime.UtcNow;
            var metadata = envelope.Metadata.WithReceivedAtUtc(receivedAtUtc);
            var normalizedEnvelope = new CommandEnvelope(envelope.Command, metadata);
            return normalizedEnvelope;
        }

        private List<CommandEnvelope> CollectReadyEnvelopes(CommandEnvelope envelope)
        {
            var readyEnvelopes = new List<CommandEnvelope>();
            var source = envelope.Metadata.Source;
            var sequence = envelope.Metadata.Sequence;
            var state = GetOrCreateSourceState(source, sequence);
            var isAlreadyProcessed = sequence < state.ExpectedSequence;
            var isFutureSequence = sequence > state.ExpectedSequence;
            if (isAlreadyProcessed)
            {
                return readyEnvelopes;
            }
            if (isFutureSequence)
            {
                QueuePendingEnvelope(state, sequence, envelope);
                return readyEnvelopes;
            }
            AddInOrderEnvelope(state, envelope, readyEnvelopes);
            FlushPendingEnvelopes(state, readyEnvelopes);
            return readyEnvelopes;
        }

        private SourceQueueState GetOrCreateSourceState(CommandSource source, long firstSeenSequence)
        {
            var hasState = sourceStates.TryGetValue(source, out var state);
            if (!hasState)
            {
                var initialExpectedSequence = ResolveInitialExpectedSequence(firstSeenSequence);
                state = new SourceQueueState(initialExpectedSequence);
                sourceStates.Add(source, state);
            }
            return state;
        }

        private long ResolveInitialExpectedSequence(long firstSeenSequence)
        {
            var bootstrapFromFirst = options.BootstrapIncomingFromFirstMessage;
            if (bootstrapFromFirst)
            {
                return firstSeenSequence;
            }
            return options.FirstIncomingSequence;
        }

        private void QueuePendingEnvelope(SourceQueueState state, long sequence, CommandEnvelope envelope)
        {
            var hasSequence = state.Pending.ContainsKey(sequence);
            if (!hasSequence)
            {
                state.Pending.Add(sequence, envelope);
            }
        }

        private void AddInOrderEnvelope(SourceQueueState state, CommandEnvelope envelope, List<CommandEnvelope> readyEnvelopes)
        {
            readyEnvelopes.Add(envelope);
            state.ExpectedSequence = state.ExpectedSequence + 1;
        }

        private void FlushPendingEnvelopes(SourceQueueState state, List<CommandEnvelope> readyEnvelopes)
        {
            var expectedSequence = state.ExpectedSequence;
            var hasPending = state.Pending.TryGetValue(expectedSequence, out var pendingEnvelope);
            while (hasPending)
            {
                state.Pending.Remove(expectedSequence);
                readyEnvelopes.Add(pendingEnvelope);
                state.ExpectedSequence = state.ExpectedSequence + 1;
                expectedSequence = state.ExpectedSequence;
                hasPending = state.Pending.TryGetValue(expectedSequence, out pendingEnvelope);
            }
        }

        private void DispatchEnvelopes(List<CommandEnvelope> envelopes)
        {
            foreach (var envelope in envelopes)
            {
                DispatchEnvelope(envelope);
            }
        }

        private void DispatchEnvelope(CommandEnvelope envelope)
        {
            var commandType = envelope.Command.GetType();
            var subscribers = GetSubscriberSnapshot(commandType);
            foreach (var subscriber in subscribers)
            {
                var shouldNotify = ShouldNotifySubscriber(subscriber, envelope.Metadata);
                if (shouldNotify)
                {
                    subscriber.Callback.Invoke(envelope.Command, envelope.Metadata);
                }
            }
        }

        private List<SubscriptionEntry> GetSubscriberSnapshot(Type commandType)
        {
            List<SubscriptionEntry> snapshot;
            lock (syncRoot)
            {
                var hasSubscribers = subscribersByType.TryGetValue(commandType, out var entries);
                if (hasSubscribers)
                {
                    snapshot = new List<SubscriptionEntry>(entries);
                }
                else
                {
                    snapshot = new List<SubscriptionEntry>();
                }
            }
            return snapshot;
        }

        private bool ShouldNotifySubscriber(SubscriptionEntry subscriber, CommandMetadata metadata)
        {
            var hasFilter = subscriber.MetadataFilter != null;
            if (hasFilter)
            {
                return subscriber.MetadataFilter(metadata);
            }
            return true;
        }

        private sealed class SubscriptionEntry
        {
            public SubscriptionEntry(Action<ICommand, CommandMetadata> callback, Predicate<CommandMetadata> metadataFilter)
            {
                Callback = callback;
                MetadataFilter = metadataFilter;
            }

            public Action<ICommand, CommandMetadata> Callback { get; }

            public Predicate<CommandMetadata> MetadataFilter { get; }
        }

        private sealed class SourceQueueState
        {
            public SourceQueueState(long expectedSequence)
            {
                ExpectedSequence = expectedSequence;
                Pending = new SortedDictionary<long, CommandEnvelope>();
            }

            public long ExpectedSequence { get; set; }

            public SortedDictionary<long, CommandEnvelope> Pending { get; }
        }
    }
}

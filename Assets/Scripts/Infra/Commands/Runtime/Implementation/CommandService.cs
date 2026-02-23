using System;
using System.Collections.Generic;

namespace Scaffold.Commands
{
    /// <summary>
    /// Instance-based command service with polymorphic subscriptions.
    /// </summary>
    public class CommandService : ICommandService, IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly ICommandTransport transport;
        private readonly List<SubscriptionEntry> subscriptions = new List<SubscriptionEntry>();
        private bool isDisposed;

        public CommandService(ICommandTransport commandTransport)
        {
            transport = ResolveTransport(commandTransport);
            transport.AddReceiver(HandleIncomingMessage);
        }

        public void Send<TMessage>(TMessage message) where TMessage : class, ICommand
        {
            EnsureNotDisposed();
            ICommand command = message;
            var transportMessage = CreateOutgoingMessage(command);
            transport.Send(transportMessage);
        }

        public IDisposable Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class, ICommand
        {
            var listener = CreateListener(handler);
            var subscription = SubscribeWithMetadata(listener);
            return subscription;
        }

        public IDisposable Subscribe<TMessage>(Action<TMessage, CommandMetadata> handler) where TMessage : class, ICommand
        {
            var subscription = SubscribeWithMetadata(handler);
            return subscription;
        }

        public IDisposable SubscribeAny(Action<ICommand> handler)
        {
            var listener = CreateAnyListener(handler);
            var subscription = SubscribeAnyWithMetadata(listener);
            return subscription;
        }

        public IDisposable SubscribeAny(Action<ICommand, CommandMetadata> handler)
        {
            var subscription = SubscribeAnyWithMetadata(handler);
            return subscription;
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                transport.RemoveReceiver(HandleIncomingMessage);
                lock (syncRoot)
                {
                    subscriptions.Clear();
                }
            }
        }

        private ICommandTransport ResolveTransport(ICommandTransport commandTransport)
        {
            var hasTransport = commandTransport != null;
            if (!hasTransport)
            {
                throw new ArgumentNullException(nameof(commandTransport));
            }
            return commandTransport;
        }

        private void EnsureNotDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(CommandService));
            }
        }

        private void HandleIncomingMessage(CommandTransportMessage message)
        {
            var hasMessage = message != null;
            var canProcess = hasMessage && !isDisposed;
            if (canProcess)
            {
                var dispatchItem = CreateDispatchItem(message);
                DispatchItem(dispatchItem);
            }
        }

        private CommandDispatchItem CreateDispatchItem(CommandTransportMessage message)
        {
            var receivedAtUtc = DateTime.UtcNow;
            var metadata = new CommandMetadata(message.MessageId, message.CreatedAtUtc, receivedAtUtc, message.CorrelationId);
            var dispatchItem = new CommandDispatchItem(message.Message, metadata);
            return dispatchItem;
        }

        private void DispatchItem(CommandDispatchItem readyItem)
        {
            var snapshot = GetSubscriptionSnapshot();
            foreach (var entry in snapshot)
            {
                var shouldNotify = ShouldNotify(entry, readyItem);
                if (shouldNotify)
                {
                    entry.Listener.Invoke(readyItem.Message, readyItem.Metadata);
                }
            }
        }

        private List<SubscriptionEntry> GetSubscriptionSnapshot()
        {
            List<SubscriptionEntry> snapshot;
            lock (syncRoot)
            {
                snapshot = new List<SubscriptionEntry>(subscriptions);
            }
            return snapshot;
        }

        private bool ShouldNotify(SubscriptionEntry entry, CommandDispatchItem readyItem)
        {
            var messageType = readyItem.Message.GetType();
            var canHandleType = entry.MessageType.IsAssignableFrom(messageType);
            return canHandleType;
        }

        private CommandTransportMessage CreateOutgoingMessage(ICommand command)
        {
            var messageId = CreateMessageId();
            var createdAtUtc = DateTime.UtcNow;
            var outgoingMessage = new CommandTransportMessage(command, messageId, createdAtUtc, string.Empty);
            return outgoingMessage;
        }

        private string CreateMessageId()
        {
            var guid = Guid.NewGuid();
            var messageId = guid.ToString("N");
            return messageId;
        }

        private Action<TMessage, CommandMetadata> CreateListener<TMessage>(Action<TMessage> handler) where TMessage : class, ICommand
        {
            ValidateHandler(handler);
            Action<TMessage, CommandMetadata> listener = (message, metadata) => handler(message);
            return listener;
        }

        private Action<ICommand, CommandMetadata> CreateAnyListener(Action<ICommand> handler)
        {
            ValidateHandler(handler);
            Action<ICommand, CommandMetadata> listener = (message, metadata) => handler(message);
            return listener;
        }

        private IDisposable SubscribeWithMetadata<TMessage>(Action<TMessage, CommandMetadata> handler) where TMessage : class, ICommand
        {
            EnsureNotDisposed();
            ValidateHandler(handler);
            Action<ICommand, CommandMetadata> listener = (message, metadata) => handler((TMessage)message, metadata);
            var messageType = typeof(TMessage);
            var subscription = AddSubscription(messageType, listener);
            return subscription;
        }

        private IDisposable SubscribeAnyWithMetadata(Action<ICommand, CommandMetadata> handler)
        {
            EnsureNotDisposed();
            ValidateHandler(handler);
            var messageType = typeof(ICommand);
            var subscription = AddSubscription(messageType, handler);
            return subscription;
        }

        private void ValidateHandler<TMessage>(Action<TMessage> handler)
        {
            var hasHandler = handler != null;
            if (!hasHandler)
            {
                throw new ArgumentNullException(nameof(handler));
            }
        }

        private void ValidateHandler<TMessage>(Action<TMessage, CommandMetadata> handler)
        {
            var hasHandler = handler != null;
            if (!hasHandler)
            {
                throw new ArgumentNullException(nameof(handler));
            }
        }

        private IDisposable AddSubscription(Type messageType, Action<ICommand, CommandMetadata> listener)
        {
            var entry = new SubscriptionEntry(messageType, listener);
            lock (syncRoot)
            {
                subscriptions.Add(entry);
            }
            var subscription = new CommandSubscription(() => RemoveSubscription(entry));
            return subscription;
        }

        private void RemoveSubscription(SubscriptionEntry entry)
        {
            if (!isDisposed)
            {
                lock (syncRoot)
                {
                    subscriptions.Remove(entry);
                }
            }
        }

        private sealed class SubscriptionEntry
        {
            public SubscriptionEntry(Type messageType, Action<ICommand, CommandMetadata> listener)
            {
                MessageType = messageType;
                Listener = listener;
            }

            public Type MessageType { get; }

            public Action<ICommand, CommandMetadata> Listener { get; }
        }
    }
}

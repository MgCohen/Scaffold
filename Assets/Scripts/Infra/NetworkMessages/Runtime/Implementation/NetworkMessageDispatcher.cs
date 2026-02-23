using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;


namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// A generic dispatcher service for sending and receiving custom typed messages over Unity Netcode.
    /// Includes sender-ordered buffering with missing-sequence recovery requests.
    /// </summary>
    public class NetworkMessageDispatcher : INetworkMessageDispatcher, IDisposable
    {
        private const int MaxCachedMessages = 1024;
        private const string MissingSequenceRequestMessageName = "__scaffold_missing_sequence_request__";
        private readonly object syncRoot = new object();
        private readonly NetworkManager networkManager;
        private readonly Dictionary<Type, object> handlers = new Dictionary<Type, object>();
        private readonly Dictionary<ulong, IncomingSenderState> incomingSenderStates = new Dictionary<ulong, IncomingSenderState>();
        private readonly Dictionary<long, ICachedOutgoingMessage> outgoingCache = new Dictionary<long, ICachedOutgoingMessage>();
        private readonly Queue<long> outgoingCacheOrder = new Queue<long>();
        private long nextOutgoingSequence = 1;
        private bool areInternalHandlersRegistered;
        private bool isDisposed;

        public NetworkMessageDispatcher()
        {
            networkManager = NetworkManager.Singleton;
            RegisterInternalHandlersIfPossible();
        }

        public void RegisterHandler<T>(Action<ulong, T> handler) where T : unmanaged
        {
            if (isDisposed) return;
            var messageName = GetMessageName(typeof(T));

            if (handlers.ContainsKey(typeof(T)))
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] A handler for {messageName} is already registered. Overwriting.");
            }

            handlers[typeof(T)] = handler;

            var messagingManager = GetMessagingManager();
            if (messagingManager != null)
            {
                RegisterInternalHandlersIfPossible(messagingManager);
                messagingManager.RegisterNamedMessageHandler(messageName, ReceiveMessage<T>);
            }
            else
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] NetworkManager.CustomMessagingManager is null when registering {messageName}. Ensure NetworkManager is correctly initialized and listening.");
            }
        }

        public void UnregisterHandler<T>() where T : unmanaged
        {
            if (isDisposed) return;
            var messageName = GetMessageName(typeof(T));

            if (handlers.Remove(typeof(T)))
            {
                var messagingManager = GetMessagingManager();
                if (messagingManager != null)
                {
                    messagingManager.UnregisterNamedMessageHandler(messageName);
                }
            }
        }

        public void SendToServer<T>(T message) where T : unmanaged
        {
            if (isDisposed) return;
            var messagingManager = GetMessagingManager();
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }
            RegisterInternalHandlersIfPossible(messagingManager);
            var messageName = GetMessageName(typeof(T));
            var sequence = ReserveOutgoingSequence();
            CacheOutgoingMessage(sequence, messageName, message);
            using var writer = CreateSequencedWriter(sequence, message);
            messagingManager.SendNamedMessage(messageName, NetworkManager.ServerClientId, writer);
        }

        public void SendToClient<T>(T message, ulong clientId) where T : unmanaged
        {
            if (isDisposed) return;
            var messagingManager = GetMessagingManager();
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }
            RegisterInternalHandlersIfPossible(messagingManager);
            var messageName = GetMessageName(typeof(T));
            var sequence = ReserveOutgoingSequence();
            CacheOutgoingMessage(sequence, messageName, message);
            using var writer = CreateSequencedWriter(sequence, message);
            messagingManager.SendNamedMessage(messageName, clientId, writer);
        }

        public void SendToClients<T>(T message, IReadOnlyList<ulong> clientIds) where T : unmanaged
        {
            if (isDisposed) return;
            var messagingManager = GetMessagingManager();
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }
            RegisterInternalHandlersIfPossible(messagingManager);
            var messageName = GetMessageName(typeof(T));
            var sequence = ReserveOutgoingSequence();
            CacheOutgoingMessage(sequence, messageName, message);
            using var writer = CreateSequencedWriter(sequence, message);
            messagingManager.SendNamedMessage(messageName, clientIds, writer);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            var messagingManager = GetMessagingManager();
            if (messagingManager != null)
            {
                UnregisterInternalHandlersIfNeeded(messagingManager);
                foreach (var handlerType in handlers.Keys)
                {
                    var messageName = GetMessageName(handlerType);
                    messagingManager.UnregisterNamedMessageHandler(messageName);
                }
            }

            handlers.Clear();
            lock (syncRoot)
            {
                incomingSenderStates.Clear();
                outgoingCache.Clear();
                outgoingCacheOrder.Clear();
            }
        }

        private void ReceiveMessage<T>(ulong senderClientId, FastBufferReader messagePayload) where T : unmanaged
        {
            if (isDisposed) return;
            if (!handlers.TryGetValue(typeof(T), out var handlerObj) || handlerObj is not Action<ulong, T> handler)
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] Received message of type {typeof(T).FullName} but no valid handler was found.");
                return;
            }
            try
            {
                messagePayload.ReadValueSafe(out ForceNetworkSerializeByMemcpy<EquatableWrapper<SequencedPayload<T>>> forceSerializable);
                var payload = forceSerializable.Value.Value;
                var pendingMessage = new PendingInboundMessage<T>(senderClientId, payload.Sequence, payload.Message, handler);
                List<IPendingInboundMessage> readyMessages;
                List<MissingSequenceRequestTarget> missingRequests;
                lock (syncRoot)
                {
                    readyMessages = new List<IPendingInboundMessage>();
                    missingRequests = new List<MissingSequenceRequestTarget>();
                    QueueInboundMessage(pendingMessage, readyMessages, missingRequests);
                }
                DispatchReadyMessages(readyMessages);
                SendMissingSequenceRequests(missingRequests);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkMessageDispatcher] Failed to deserialize or handle message of type {typeof(T).FullName}:\n{e}");
            }
        }

        private void QueueInboundMessage(IPendingInboundMessage pendingMessage, List<IPendingInboundMessage> readyMessages, List<MissingSequenceRequestTarget> missingRequests)
        {
            var senderState = GetOrCreateIncomingSenderState(pendingMessage.SenderClientId, pendingMessage.Sequence);
            var isStaleMessage = pendingMessage.Sequence < senderState.ExpectedSequence;
            if (isStaleMessage)
            {
                return;
            }
            var isFutureMessage = pendingMessage.Sequence > senderState.ExpectedSequence;
            if (isFutureMessage)
            {
                CachePendingInboundMessage(senderState, pendingMessage);
                TryQueueMissingSequenceRequest(pendingMessage.SenderClientId, senderState.ExpectedSequence, senderState, missingRequests);
                return;
            }
            AddReadyMessage(senderState, pendingMessage, readyMessages);
            FlushPendingInboundMessages(senderState, readyMessages);
            TryQueueMissingSequenceRequestForGap(pendingMessage.SenderClientId, senderState, missingRequests);
        }

        private IncomingSenderState GetOrCreateIncomingSenderState(ulong senderClientId, long firstSeenSequence)
        {
            var hasState = incomingSenderStates.TryGetValue(senderClientId, out var senderState);
            if (!hasState)
            {
                senderState = new IncomingSenderState(firstSeenSequence);
                incomingSenderStates.Add(senderClientId, senderState);
            }
            return senderState;
        }

        private void CachePendingInboundMessage(IncomingSenderState senderState, IPendingInboundMessage pendingMessage)
        {
            var hasPendingMessage = senderState.PendingMessages.ContainsKey(pendingMessage.Sequence);
            if (!hasPendingMessage)
            {
                senderState.PendingMessages.Add(pendingMessage.Sequence, pendingMessage);
            }
        }

        private void AddReadyMessage(IncomingSenderState senderState, IPendingInboundMessage pendingMessage, List<IPendingInboundMessage> readyMessages)
        {
            readyMessages.Add(pendingMessage);
            senderState.RequestedSequences.Remove(pendingMessage.Sequence);
            senderState.ExpectedSequence = senderState.ExpectedSequence + 1;
        }

        private void FlushPendingInboundMessages(IncomingSenderState senderState, List<IPendingInboundMessage> readyMessages)
        {
            var hasPendingNext = senderState.PendingMessages.TryGetValue(senderState.ExpectedSequence, out var pendingMessage);
            while (hasPendingNext)
            {
                senderState.PendingMessages.Remove(senderState.ExpectedSequence);
                readyMessages.Add(pendingMessage);
                senderState.RequestedSequences.Remove(senderState.ExpectedSequence);
                senderState.ExpectedSequence = senderState.ExpectedSequence + 1;
                hasPendingNext = senderState.PendingMessages.TryGetValue(senderState.ExpectedSequence, out pendingMessage);
            }
        }

        private void TryQueueMissingSequenceRequestForGap(ulong senderClientId, IncomingSenderState senderState, List<MissingSequenceRequestTarget> missingRequests)
        {
            var hasPendingMessages = senderState.PendingMessages.Count > 0;
            if (hasPendingMessages)
            {
                var smallestPendingSequence = GetSmallestPendingSequence(senderState.PendingMessages);
                var hasGap = smallestPendingSequence > senderState.ExpectedSequence;
                if (hasGap)
                {
                    TryQueueMissingSequenceRequest(senderClientId, senderState.ExpectedSequence, senderState, missingRequests);
                }
            }
        }

        private long GetSmallestPendingSequence(SortedDictionary<long, IPendingInboundMessage> pendingMessages)
        {
            foreach (var pendingMessage in pendingMessages)
            {
                return pendingMessage.Key;
            }
            return long.MaxValue;
        }

        private void TryQueueMissingSequenceRequest(ulong senderClientId, long missingSequence, IncomingSenderState senderState, List<MissingSequenceRequestTarget> missingRequests)
        {
            var wasQueued = senderState.RequestedSequences.Add(missingSequence);
            if (wasQueued)
            {
                var request = new MissingSequenceRequestTarget(senderClientId, missingSequence);
                missingRequests.Add(request);
            }
        }

        private void DispatchReadyMessages(List<IPendingInboundMessage> readyMessages)
        {
            foreach (var pendingMessage in readyMessages)
            {
                pendingMessage.Dispatch();
            }
        }

        private void SendMissingSequenceRequests(List<MissingSequenceRequestTarget> missingRequests)
        {
            foreach (var request in missingRequests)
            {
                SendMissingSequenceRequest(request.SenderClientId, request.MissingSequence);
            }
        }

        private void SendMissingSequenceRequest(ulong senderClientId, long missingSequence)
        {
            var messagingManager = GetMessagingManager();
            if (messagingManager == null)
            {
                return;
            }
            RegisterInternalHandlersIfPossible(messagingManager);
            var request = new MissingSequenceRequest(missingSequence);
            using var writer = CreateWriter(request);
            messagingManager.SendNamedMessage(MissingSequenceRequestMessageName, senderClientId, writer);
        }

        private void ReceiveMissingSequenceRequest(ulong requesterClientId, FastBufferReader messagePayload)
        {
            if (isDisposed) return;
            try
            {
                messagePayload.ReadValueSafe(out ForceNetworkSerializeByMemcpy<EquatableWrapper<MissingSequenceRequest>> forceSerializable);
                var request = forceSerializable.Value.Value;
                TryResendCachedMessage(requesterClientId, request.MissingSequence);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkMessageDispatcher] Failed to process missing-sequence request:\n{e}");
            }
        }

        private void TryResendCachedMessage(ulong requesterClientId, long sequence)
        {
            ICachedOutgoingMessage cachedMessage;
            lock (syncRoot)
            {
                var hasCachedMessage = outgoingCache.TryGetValue(sequence, out cachedMessage);
                if (!hasCachedMessage)
                {
                    return;
                }
            }
            var messagingManager = GetMessagingManager();
            if (messagingManager == null)
            {
                return;
            }
            RegisterInternalHandlersIfPossible(messagingManager);
            cachedMessage.Resend(messagingManager, requesterClientId);
        }

        private long ReserveOutgoingSequence()
        {
            long sequence;
            lock (syncRoot)
            {
                sequence = nextOutgoingSequence;
                nextOutgoingSequence = nextOutgoingSequence + 1;
            }
            return sequence;
        }

        private void CacheOutgoingMessage<T>(long sequence, string messageName, T message) where T : unmanaged
        {
            var cachedMessage = new CachedOutgoingMessage<T>(sequence, messageName, message);
            lock (syncRoot)
            {
                outgoingCache[sequence] = cachedMessage;
                outgoingCacheOrder.Enqueue(sequence);
                TrimOutgoingCache();
            }
        }

        private void TrimOutgoingCache()
        {
            while (outgoingCache.Count > MaxCachedMessages && outgoingCacheOrder.Count > 0)
            {
                var oldestSequence = outgoingCacheOrder.Dequeue();
                outgoingCache.Remove(oldestSequence);
            }
        }

        private void RegisterInternalHandlersIfPossible()
        {
            var messagingManager = GetMessagingManager();
            if (messagingManager != null)
            {
                RegisterInternalHandlersIfPossible(messagingManager);
            }
        }

        private void RegisterInternalHandlersIfPossible(CustomMessagingManager messagingManager)
        {
            if (areInternalHandlersRegistered)
            {
                return;
            }
            messagingManager.RegisterNamedMessageHandler(MissingSequenceRequestMessageName, ReceiveMissingSequenceRequest);
            areInternalHandlersRegistered = true;
        }

        private void UnregisterInternalHandlersIfNeeded(CustomMessagingManager messagingManager)
        {
            if (!areInternalHandlersRegistered)
            {
                return;
            }
            messagingManager.UnregisterNamedMessageHandler(MissingSequenceRequestMessageName);
            areInternalHandlersRegistered = false;
        }

        private CustomMessagingManager GetMessagingManager()
        {
            var messagingManager = networkManager?.CustomMessagingManager;
            return messagingManager;
        }

        private string GetMessageName(Type messageType)
        {
            var messageName = messageType.FullName;
            var hasMessageName = !string.IsNullOrWhiteSpace(messageName);
            if (!hasMessageName)
            {
                throw new InvalidOperationException($"Unable to resolve message name for type {messageType}.");
            }
            return messageName;
        }

        private static FastBufferWriter CreateSequencedWriter<T>(long sequence, T message) where T : unmanaged
        {
            var sequencedPayload = new SequencedPayload<T>(sequence, message);
            var writer = CreateWriter(sequencedPayload);
            return writer;
        }

        private static FastBufferWriter CreateWriter<T>(T message) where T : unmanaged
        {
            int size;
            try
            {
                size = Marshal.SizeOf(typeof(T));
                size += 16;
            }
            catch
            {
                size = 256;
            }
            var writer = new FastBufferWriter(size, Unity.Collections.Allocator.Temp);
            var genericWrapper = new EquatableWrapper<T>(message);
            var forceSerializable = new ForceNetworkSerializeByMemcpy<EquatableWrapper<T>>(genericWrapper);
            writer.WriteValueSafe(in forceSerializable);
            return writer;
        }

        private struct SequencedPayload<T> where T : unmanaged
        {
            public SequencedPayload(long sequence, T message)
            {
                Sequence = sequence;
                Message = message;
            }

            public long Sequence;

            public T Message;
        }

        private struct MissingSequenceRequest
        {
            public MissingSequenceRequest(long missingSequence)
            {
                MissingSequence = missingSequence;
            }

            public long MissingSequence;
        }

        private readonly struct MissingSequenceRequestTarget
        {
            public MissingSequenceRequestTarget(ulong senderClientId, long missingSequence)
            {
                SenderClientId = senderClientId;
                MissingSequence = missingSequence;
            }

            public ulong SenderClientId { get; }

            public long MissingSequence { get; }
        }

        private interface IPendingInboundMessage
        {
            ulong SenderClientId { get; }

            long Sequence { get; }

            void Dispatch();
        }

        private sealed class PendingInboundMessage<T> : IPendingInboundMessage where T : unmanaged
        {
            private readonly T message;
            private readonly Action<ulong, T> handler;

            public PendingInboundMessage(ulong senderClientId, long sequence, T incomingMessage, Action<ulong, T> incomingHandler)
            {
                SenderClientId = senderClientId;
                Sequence = sequence;
                message = incomingMessage;
                handler = incomingHandler;
            }

            public ulong SenderClientId { get; }

            public long Sequence { get; }

            public void Dispatch()
            {
                handler.Invoke(SenderClientId, message);
            }
        }

        private sealed class IncomingSenderState
        {
            public IncomingSenderState(long expectedSequence)
            {
                ExpectedSequence = expectedSequence;
                PendingMessages = new SortedDictionary<long, IPendingInboundMessage>();
                RequestedSequences = new HashSet<long>();
            }

            public long ExpectedSequence { get; set; }

            public SortedDictionary<long, IPendingInboundMessage> PendingMessages { get; }

            public HashSet<long> RequestedSequences { get; }
        }

        private interface ICachedOutgoingMessage
        {
            void Resend(CustomMessagingManager messagingManager, ulong targetClientId);
        }

        private sealed class CachedOutgoingMessage<T> : ICachedOutgoingMessage where T : unmanaged
        {
            private readonly long sequence;
            private readonly string messageName;
            private readonly T message;

            public CachedOutgoingMessage(long outgoingSequence, string outgoingMessageName, T outgoingMessage)
            {
                sequence = outgoingSequence;
                messageName = outgoingMessageName;
                message = outgoingMessage;
            }

            public void Resend(CustomMessagingManager messagingManager, ulong targetClientId)
            {
                using var writer = CreateSequencedWriter(sequence, message);
                messagingManager.SendNamedMessage(messageName, targetClientId, writer);
            }
        }
    }
}

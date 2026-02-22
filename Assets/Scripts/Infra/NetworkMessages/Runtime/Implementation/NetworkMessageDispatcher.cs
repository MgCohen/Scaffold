using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;


namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// A generic dispatcher service for sending and receiving custom typed messages over Unity Netcode.
    /// Eliminates reflection dynamically by wrapping all unmanaged struct types inside ForceNetworkSerializeByMemcpy.
    /// </summary>
    public class NetworkMessageDispatcher : INetworkMessageDispatcher, IDisposable
    {
        private readonly NetworkManager networkManager;
        private readonly Dictionary<Type, object> handlers = new Dictionary<Type, object>();
        private bool isDisposed;

        public NetworkMessageDispatcher(NetworkManager networkManager)
        {
            this.networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
        }

        public void RegisterHandler<T>(Action<ulong, T> handler) where T : unmanaged
        {
            if (isDisposed) return;

            var messageName = typeof(T).FullName;

            if (handlers.ContainsKey(typeof(T)))
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] A handler for {messageName} is already registered. Overwriting.");
            }

            handlers[typeof(T)] = handler;

            var messagingManager = networkManager.CustomMessagingManager;
            if (messagingManager != null)
            {
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

            var messageName = typeof(T).FullName;

            if (handlers.Remove(typeof(T)))
            {
                var messagingManager = networkManager.CustomMessagingManager;
                if (messagingManager != null)
                {
                    messagingManager.UnregisterNamedMessageHandler(messageName);
                }
            }
        }

        private FastBufferWriter CreateWriter<T>(T message) where T : unmanaged
        {
            int size;
            try
            {
                size = Marshal.SizeOf(typeof(T));
                size += 16;
            }
            catch
            {
                size = 256; // Fallback capacity
            }

            var writer = new FastBufferWriter(size, Unity.Collections.Allocator.Temp);
            var genericWrapper = new EquatableWrapper<T>(message);
            var forceSerializable = new ForceNetworkSerializeByMemcpy<EquatableWrapper<T>>(genericWrapper);
            writer.WriteValueSafe(in forceSerializable);
            return writer;
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
                messagePayload.ReadValueSafe(out ForceNetworkSerializeByMemcpy<EquatableWrapper<T>> forceSerializable);
                T message = forceSerializable.Value.Value;
                handler.Invoke(senderClientId, message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkMessageDispatcher] Failed to deserialize or handle message of type {typeof(T).FullName}:\n{e}");
            }
        }

        public void SendToServer<T>(T message) where T : unmanaged
        {
            if (isDisposed) return;

            var messagingManager = networkManager.CustomMessagingManager;
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }

            using var writer = CreateWriter(message);
            messagingManager.SendNamedMessage(typeof(T).FullName, NetworkManager.ServerClientId, writer);
        }

        public void SendToClient<T>(T message, ulong clientId) where T : unmanaged
        {
            if (isDisposed) return;

            var messagingManager = networkManager.CustomMessagingManager;
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }

            using var writer = CreateWriter(message);
            messagingManager.SendNamedMessage(typeof(T).FullName, clientId, writer);
        }

        public void SendToClients<T>(T message, IReadOnlyList<ulong> clientIds) where T : unmanaged
        {
            if (isDisposed) return;

            var messagingManager = networkManager.CustomMessagingManager;
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }

            using var writer = CreateWriter(message);
            messagingManager.SendNamedMessage(typeof(T).FullName, clientIds, writer);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            var messagingManager = networkManager?.CustomMessagingManager;
            if (messagingManager != null)
            {
                foreach (var handlerType in handlers.Keys)
                {
                    messagingManager.UnregisterNamedMessageHandler(handlerType.FullName);
                }
            }

            handlers.Clear();
        }
    }
}

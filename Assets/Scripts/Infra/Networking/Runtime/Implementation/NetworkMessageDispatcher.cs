using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;
using Infra.Networking.Runtime.Abstractions;
using Infra.Networking.Runtime.Models;

namespace Infra.Networking.Runtime.Implementation
{
    /// <summary>
    /// A generic dispatcher service for sending and receiving custom typed messages over Unity Netcode.
    /// Eliminates reflection dynamically by wrapping all unmanaged struct types inside ForceNetworkSerializeByMemcpy.
    /// </summary>
    public class NetworkMessageDispatcher : INetworkMessageDispatcher, IDisposable
    {
        private readonly NetworkManager m_NetworkManager;
        private readonly Dictionary<Type, object> m_Handlers = new Dictionary<Type, object>();
        private bool m_IsDisposed;

        public NetworkMessageDispatcher(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
        }

        public void RegisterHandler<T>(Action<ulong, T> handler) where T : unmanaged
        {
            if (m_IsDisposed) return;

            var messageName = typeof(T).FullName;

            if (m_Handlers.ContainsKey(typeof(T)))
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] A handler for {messageName} is already registered. Overwriting.");
            }

            m_Handlers[typeof(T)] = handler;

            var messagingManager = m_NetworkManager.CustomMessagingManager;
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
            if (m_IsDisposed) return;

            var messageName = typeof(T).FullName;

            if (m_Handlers.Remove(typeof(T)))
            {
                var messagingManager = m_NetworkManager.CustomMessagingManager;
                if (messagingManager != null)
                {
                    messagingManager.UnregisterNamedMessageHandler(messageName);
                }
            }
        }

        /// <summary>
        /// Extracted generic conversion method to safely wrap the unmanaged logic inside FastBufferWriter layers.
        /// It bypasses Reflection by enforcing `EquatableWrapper<T>` for all types.
        /// </summary>
        private FastBufferWriter CreateWriter<T>(T message) where T : unmanaged
        {
            int size;
            try
            {
                size = Marshal.SizeOf(typeof(T));
                // Add some safety padding for Netcode's ForceNetworkSerializeByMemcpy headers
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
            if (m_IsDisposed) return;

            if (!m_Handlers.TryGetValue(typeof(T), out var handlerObj) || handlerObj is not Action<ulong, T> handler)
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] Received message of type {typeof(T).FullName} but no valid handler was found.");
                return;
            }
            try
            {
                // Read the payload back through the identical Equitable layered wrapper we wrote it with
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
            if (m_IsDisposed) return;

            var messagingManager = m_NetworkManager.CustomMessagingManager;
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
            if (m_IsDisposed) return;

            var messagingManager = m_NetworkManager.CustomMessagingManager;
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
            if (m_IsDisposed) return;

            var messagingManager = m_NetworkManager.CustomMessagingManager;
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
            if (m_IsDisposed) return;
            m_IsDisposed = true;

            var messagingManager = m_NetworkManager?.CustomMessagingManager;
            if (messagingManager != null)
            {
                foreach (var handlerType in m_Handlers.Keys)
                {
                    messagingManager.UnregisterNamedMessageHandler(handlerType.FullName);
                }
            }

            m_Handlers.Clear();
        }
    }
}

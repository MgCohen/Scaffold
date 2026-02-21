using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;

namespace Infra.Networking
{
    /// <summary>
    /// A generic wrapper struct that ensures our type is IEquatable for ForceNetworkSerializeByMemcpy.
    /// This is used for types that do not implement INetworkSerializable natively to ensure safe serialization.
    /// </summary>
    public struct EquatableWrapper<T> : IEquatable<EquatableWrapper<T>> where T : unmanaged
    {
        public T Value;

        public EquatableWrapper(T value)
        {
            Value = value;
        }

        public bool Equals(EquatableWrapper<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }
    }

    /// <summary>
    /// A generic dispatcher for sending and receiving custom typed messages over Unity Netcode.
    /// </summary>
    public static class NetworkMessageDispatcher
    {
        private static readonly Dictionary<Type, object> s_Handlers = new Dictionary<Type, object>();

        /// <summary>
        /// Registers a handler for a specific unmanaged message type <typeparamref name="T"/>.
        /// </summary>
        public static void RegisterHandler<T>(Action<ulong, T> handler) where T : unmanaged
        {
            var messageName = typeof(T).FullName;
            
            if (s_Handlers.ContainsKey(typeof(T)))
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] A handler for {messageName} is already registered. Overwriting.");
            }

            s_Handlers[typeof(T)] = handler;

            var messagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (messagingManager != null)
            {
                messagingManager.RegisterNamedMessageHandler(messageName, ReceiveMessage<T>);
            }
            else
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] NetworkManager.CustomMessagingManager is null when registering {messageName}. Ensure NetworkManager is initialized.");
            }
        }

        /// <summary>
        /// Unregisters a previously registered handler for the unmanaged message type <typeparamref name="T"/>.
        /// </summary>
        public static void UnregisterHandler<T>() where T : unmanaged
        {
            var messageName = typeof(T).FullName;

            if (s_Handlers.Remove(typeof(T)))
            {
                var messagingManager = NetworkManager.Singleton?.CustomMessagingManager;
                if (messagingManager != null)
                {
                    messagingManager.UnregisterNamedMessageHandler(messageName);
                }
            }
        }

        /// <summary>
        /// Extracted generic conversion method to safely wrap the unmanaged logic inside FastBufferWriter layers.
        /// </summary>
        private static FastBufferWriter CreateWriter<T>(T message) where T : unmanaged
        {
            // Discover payload size constraint using Marshal.SizeOf as instructed
            int size;
            try
            {
                size = Marshal.SizeOf(typeof(T));
            }
            catch
            {
                size = 256; // Fallback capacity
            }

            var writer = new FastBufferWriter(size, Unity.Collections.Allocator.Temp);

            // Layer 1: Check if it's INetworkSerializable
            if (message is INetworkSerializable serializable)
            {
                writer.WriteNetworkSerializable(serializable);
            }
            // Layer 2: Otherwise use safe ForceNetworkSerializeByMemcpy layers by ensuring an IEquatable constraint
            else
            {
                var genericWrapper = new EquatableWrapper<T>(message);
                var forceSerializable = new ForceNetworkSerializeByMemcpy<EquatableWrapper<T>>(genericWrapper);
                writer.WriteValueSafe(in forceSerializable);
            }

            return writer;
        }

        private static void ReceiveMessage<T>(ulong senderClientId, FastBufferReader messagePayload) where T : unmanaged
        {
            if (!s_Handlers.TryGetValue(typeof(T), out var handlerObj) || handlerObj is not Action<ulong, T> handler)
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] Received message of type {typeof(T).FullName} but no valid handler was found.");
                return;
            }

            try
            {
                T message = default;

                if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
                {
                    // Use reflection dynamically to construct the reader correctly for INetworkSerializable
                    // Since the struct passes the constraints and has a new() parameterless capability.
                    var method = typeof(FastBufferReader).GetMethod(nameof(FastBufferReader.ReadNetworkSerializable), BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        var genericMethod = method.MakeGenericMethod(typeof(T));
                        
                        object[] parameters = new object[] { null };
                        // Note: reflection on struct (FastBufferReader doesn't throw, but it operates on boxed instance).
                        // Since we just read completely, it doesn't matter since the reader reference is localized here.
                        object boxedReader = messagePayload;
                        genericMethod.Invoke(boxedReader, parameters);
                        
                        if (parameters[0] != null)
                        {
                            message = (T)parameters[0];
                        }
                    }
                }
                else
                {
                    // Read the payload back through the identical Equitable layered wrapper we wrote it with
                    messagePayload.ReadValueSafe(out ForceNetworkSerializeByMemcpy<EquatableWrapper<T>> forceSerializable);
                    message = forceSerializable.Value.Value;
                }

                handler.Invoke(senderClientId, message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkMessageDispatcher] Failed to deserialize or handle message of type {typeof(T).FullName}:\n{e}");
            }
        }

        public static void SendToServer<T>(T message) where T : unmanaged
        {
            var messagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }

            using var writer = CreateWriter(message);
            messagingManager.SendNamedMessage(typeof(T).FullName, NetworkManager.ServerClientId, writer);
        }

        public static void SendToClient<T>(T message, ulong clientId) where T : unmanaged
        {
            var messagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }

            using var writer = CreateWriter(message);
            messagingManager.SendNamedMessage(typeof(T).FullName, clientId, writer);
        }

        public static void SendToClients<T>(T message, IReadOnlyList<ulong> clientIds) where T : unmanaged
        {
            var messagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (messagingManager == null)
            {
                Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
                return;
            }

            using var writer = CreateWriter(message);
            messagingManager.SendNamedMessage(typeof(T).FullName, clientIds, writer);
        }
    }
}

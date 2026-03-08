using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;


namespace Scaffold.NetworkMessages
{
    public class NetworkMessageDispatcher : INetworkMessageDispatcher, IDisposable
    {
        private readonly NetworkManager networkManager;
        private readonly Dictionary<Type, object> handlers = new Dictionary<Type, object>();
        private bool isDisposed;

        public NetworkMessageDispatcher()
        {
            networkManager = NetworkManager.Singleton;
        }

        public void RegisterHandler<T>(Action<ulong, T> handler) where T : unmanaged
        {
            if (isDisposed) return;
            string messageName = typeof(T).FullName;
            WarnIfHandlerExists<T>(messageName);
            handlers[typeof(T)] = handler;
            RegisterMessagingHandler<T>(messageName);
        }

        public void UnregisterHandler<T>() where T : unmanaged
        {
            if (isDisposed) return;
            string messageName = typeof(T).FullName;
            if (handlers.Remove(typeof(T)))
            {
                UnregisterMessagingHandler(messageName);
            }
        }

        public void SendToServer<T>(T message) where T : unmanaged
        {
            if (isDisposed) return;
            CustomMessagingManager messaging = networkManager.CustomMessagingManager;
            if (!TryValidateMessagingManager(messaging)) return;
            using var writer = CreateWriter(message);
            messaging.SendNamedMessage(typeof(T).FullName, NetworkManager.ServerClientId, writer);
        }

        public void SendToClient<T>(T message, ulong clientId) where T : unmanaged
        {
            if (isDisposed) return;
            CustomMessagingManager messaging = networkManager.CustomMessagingManager;
            if (!TryValidateMessagingManager(messaging)) return;
            using var writer = CreateWriter(message);
            messaging.SendNamedMessage(typeof(T).FullName, clientId, writer);
        }

        public void SendToClients<T>(T message, IReadOnlyList<ulong> clientIds) where T : unmanaged
        {
            if (isDisposed) return;
            CustomMessagingManager messaging = networkManager.CustomMessagingManager;
            if (!TryValidateMessagingManager(messaging)) return;
            using var writer = CreateWriter(message);
            messaging.SendNamedMessage(typeof(T).FullName, clientIds, writer);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            UnregisterAllHandlers();
            handlers.Clear();
        }

        private bool TryValidateMessagingManager(CustomMessagingManager messaging)
        {
            if (messaging != null) return true;
            Debug.LogError("[NetworkMessageDispatcher] Cannot send message: CustomMessagingManager is null.");
            return false;
        }

        private FastBufferWriter CreateWriter<T>(T message) where T : unmanaged
        {
            int size = CalculateBufferSize<T>();
            FastBufferWriter writer = new FastBufferWriter(size, Unity.Collections.Allocator.Temp);
            EquatableWrapper<T> genericWrapper = new EquatableWrapper<T>(message);
            ForceNetworkSerializeByMemcpy<EquatableWrapper<T>> forceSerializable = new ForceNetworkSerializeByMemcpy<EquatableWrapper<T>>(genericWrapper);
            writer.WriteValueSafe(in forceSerializable);
            return writer;
        }

        private int CalculateBufferSize<T>() where T : unmanaged
        {
            try
            {
                return Marshal.SizeOf(typeof(T)) + 16;
            }
            catch
            {
                return 256;
            }
        }

        private void ReceiveMessage<T>(ulong senderClientId, FastBufferReader messagePayload) where T : unmanaged
        {
            if (isDisposed) return;
            if (!TryGetHandler<T>(out Action<ulong, T> handler)) return;
            TryDeserializeAndHandle(senderClientId, messagePayload, handler);
        }

        private bool TryGetHandler<T>(out Action<ulong, T> handler) where T : unmanaged
        {
            if (handlers.TryGetValue(typeof(T), out object handlerObj) && handlerObj is Action<ulong, T> typedHandler)
            {
                handler = typedHandler;
                return true;
            }
            Debug.LogWarning($"[NetworkMessageDispatcher] Received message of type {typeof(T).FullName} but no valid handler was found.");
            handler = null;
            return false;
        }

        private void TryDeserializeAndHandle<T>(ulong senderClientId, FastBufferReader messagePayload, Action<ulong, T> handler) where T : unmanaged
        {
            try
            {
                DeserializeAndHandle(senderClientId, messagePayload, handler);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkMessageDispatcher] Failed to deserialize or handle message of type {typeof(T).FullName}:\n{e}");
            }
        }

        private void DeserializeAndHandle<T>(ulong senderClientId, FastBufferReader messagePayload, Action<ulong, T> handler) where T : unmanaged
        {
            messagePayload.ReadValueSafe(out ForceNetworkSerializeByMemcpy<EquatableWrapper<T>> forceSerializable);
            T message = forceSerializable.Value.Value;
            handler.Invoke(senderClientId, message);
        }

        private void WarnIfHandlerExists<T>(string messageName)
        {
            if (handlers.ContainsKey(typeof(T)))
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] A handler for {messageName} is already registered. Overwriting.");
            }
        }

        private void RegisterMessagingHandler<T>(string messageName) where T : unmanaged
        {
            CustomMessagingManager messagingManager = networkManager.CustomMessagingManager;
            if (messagingManager == null)
            {
                Debug.LogWarning($"[NetworkMessageDispatcher] NetworkManager.CustomMessagingManager is null when registering {messageName}. Ensure NetworkManager is correctly initialized and listening.");
                return;
            }
            messagingManager.RegisterNamedMessageHandler(messageName, ReceiveMessage<T>);
        }

        private void UnregisterMessagingHandler(string messageName)
        {
            CustomMessagingManager messagingManager = networkManager.CustomMessagingManager;
            if (messagingManager != null)
            {
                messagingManager.UnregisterNamedMessageHandler(messageName);
            }
        }

        private void UnregisterAllHandlers()
        {
            CustomMessagingManager messagingManager = networkManager?.CustomMessagingManager;
            if (messagingManager == null) return;
            foreach (Type handlerType in handlers.Keys)
            {
                messagingManager.UnregisterNamedMessageHandler(handlerType.FullName);
            }
        }
    }
}

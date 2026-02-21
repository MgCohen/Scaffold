using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Infra.Networking.Runtime.Abstractions;
using Infra.Networking.Runtime.Implementation;

namespace Infra.Networking.Samples
{
    public struct SampleMessage
    {
        public int Id;
        public float Timestamp;
    }

    /// <summary>
    /// A simple sample demonstrating how to send and receive typed custom messages using the service.
    /// Requirements: Attach to a NetworkObject in the scene or an active GameObject while NetworkManager is active.
    /// </summary>
    public class NetworkMessagingSample : NetworkBehaviour
    {
        [Tooltip("Check this in the inspector or at runtime to send a test message.")]
        public bool triggerMessageSend = false;

        private int m_MessageCounter = 0;
        private INetworkMessageDispatcher m_Dispatcher;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // In a real application, inject INetworkMessageDispatcher via an Installer/Container.
            m_Dispatcher = new NetworkMessageDispatcher(NetworkManager.Singleton);
            m_Dispatcher.RegisterHandler<SampleMessage>(OnSampleMessageReceived);
        }

        public override void OnNetworkDespawn()
        {
            if (m_Dispatcher != null)
            {
                m_Dispatcher.UnregisterHandler<SampleMessage>();
                if (m_Dispatcher is System.IDisposable disposable)
                {
                    disposable.Dispose();
                }
                m_Dispatcher = null;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (IsServer)
            {
                if (Input.GetKeyDown(KeyCode.A))
                {
                    SendSampleMessage();
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.S))
                {
                    SendSampleMessage();
                }
            }
        }

        public void SendSampleMessage()
        {
            if (!IsSpawned || m_Dispatcher == null) return;

            var message = new SampleMessage
            {
                Id = ++m_MessageCounter,
                Timestamp = Time.time
            };

            if (IsServer)
            {
                Debug.Log($"[Server] Sending SampleMessage ID: {message.Id} to all clients...");
                
                List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
                m_Dispatcher.SendToClients(message, clientIds);
            }
            else if (IsClient)
            {
                Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Sending SampleMessage ID: {message.Id} to server...");
                m_Dispatcher.SendToServer(message);
            }
        }

        private void OnSampleMessageReceived(ulong senderClientId, SampleMessage message)
        {
            Debug.Log($"{(IsServer ? "[Server]" : $"[Client {NetworkManager.Singleton.LocalClientId}]")} Received SampleMessage ID: {message.Id}, Timestamp: {message.Timestamp} from Client: {senderClientId}");
            
            if (IsServer && m_Dispatcher != null)
            {
                Debug.Log($"[Server] Bouncing message {message.Id} from {senderClientId} to everyone else.");
                
                List<ulong> otherClients = new List<ulong>();
                foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    if (clientId != senderClientId)
                        otherClients.Add(clientId);
                }

                if (otherClients.Count > 0)
                {
                    m_Dispatcher.SendToClients(message, otherClients);
                }
            }
        }
    }
}

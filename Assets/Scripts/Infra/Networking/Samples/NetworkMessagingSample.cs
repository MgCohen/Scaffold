using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Infra.Networking.Samples
{
    public struct SampleMessage
    {
        public int Id;
        public float Timestamp;
    }

    /// <summary>
    /// A simple sample demonstrating how to send and receive typed custom messages.
    /// Requirements: Attach to a NetworkObject in the scene or an active GameObject while NetworkManager is active.
    /// </summary>
    public class NetworkMessagingSample : NetworkBehaviour
    {
        [Tooltip("Check this in the inspector or at runtime to send a test message.")]
        public bool triggerMessageSend = false;

        private int m_MessageCounter = 0;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Register handler for our custom SampleMessage.
            // In a real application, make sure to handle re-registration carefully or do this only once per client/server lifetime.
            NetworkMessageDispatcher.RegisterHandler<SampleMessage>(OnSampleMessageReceived);
        }

        public override void OnNetworkDespawn()
        {
            // Clean up handler when object is despawned if no one else needs it.
            NetworkMessageDispatcher.UnregisterHandler<SampleMessage>();

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (triggerMessageSend)
            {
                triggerMessageSend = false;
                SendSampleMessage();
            }
        }

        public void SendSampleMessage()
        {
            if (!IsSpawned) return;

            var message = new SampleMessage
            {
                Id = ++m_MessageCounter,
                Timestamp = Time.time
            };

            if (IsServer)
            {
                Debug.Log($"[Server] Sending SampleMessage ID: {message.Id} to all clients...");
                
                // Example of sending to all connected clients:
                List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
                NetworkMessageDispatcher.SendToClients(message, clientIds);
                
                // Alternative: if you want to send to a specific client:
                // NetworkMessageDispatcher.SendToClient(message, specificClientId);
            }
            else if (IsClient)
            {
                Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Sending SampleMessage ID: {message.Id} to server...");
                NetworkMessageDispatcher.SendToServer(message);
            }
        }

        private void OnSampleMessageReceived(ulong senderClientId, SampleMessage message)
        {
            Debug.Log($"{(IsServer ? "[Server]" : $"[Client {NetworkManager.Singleton.LocalClientId}]")} Received SampleMessage ID: {message.Id}, Timestamp: {message.Timestamp} from Client: {senderClientId}");
            
            // Example Server Reflection: Server receives from client, logs it, and bounces it to all other clients
            if (IsServer)
            {
                // In a real scenario, validate data before resending
                Debug.Log($"[Server] Bouncing message {message.Id} from {senderClientId} to everyone else.");
                
                List<ulong> otherClients = new List<ulong>();
                foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    if (clientId != senderClientId)
                        otherClients.Add(clientId);
                }

                if (otherClients.Count > 0)
                {
                    NetworkMessageDispatcher.SendToClients(message, otherClients);
                }
            }
        }
    }
}

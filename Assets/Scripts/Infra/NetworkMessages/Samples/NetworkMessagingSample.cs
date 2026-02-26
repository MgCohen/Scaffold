using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Scaffold.NetworkMessages;

namespace Scaffold.NetworkMessages.Samples
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
        public bool TriggerMessageSend = false;

        private int messageCounter = 0;
        private INetworkMessageDispatcher dispatcher;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            dispatcher = new NetworkMessageDispatcher();
            dispatcher.RegisterHandler<SampleMessage>(OnSampleMessageReceived);
        }

        public override void OnNetworkDespawn()
        {
            if (dispatcher != null)
            {
                dispatcher.UnregisterHandler<SampleMessage>();
                if (dispatcher is System.IDisposable disposable)
                {
                    disposable.Dispose();
                }
                dispatcher = null;
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
            if (!IsSpawned || dispatcher == null) return;

            var message = new SampleMessage
            {
                Id = ++messageCounter,
                Timestamp = Time.time
            };

            if (IsServer)
            {
                Debug.Log($"[Server] Sending SampleMessage ID: {message.Id} to all clients...");
                
                List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
                dispatcher.SendToClients(message, clientIds);
            }
            else if (IsClient)
            {
                Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Sending SampleMessage ID: {message.Id} to server...");
                dispatcher.SendToServer(message);
            }
        }

        private void OnSampleMessageReceived(ulong senderClientId, SampleMessage message)
        {
            Debug.Log($"{(IsServer ? "[Server]" : $"[Client {NetworkManager.Singleton.LocalClientId}]")} Received SampleMessage ID: {message.Id}, Timestamp: {message.Timestamp} from Client: {senderClientId}");
            
            if (IsServer && dispatcher != null)
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
                    dispatcher.SendToClients(message, otherClients);
                }
            }
        }
    }
}

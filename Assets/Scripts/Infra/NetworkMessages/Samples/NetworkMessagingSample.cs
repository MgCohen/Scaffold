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
            CleanupDispatcher();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (IsServer)
            {
                HandleServerInput();
            }
            else
            {
                HandleClientInput();
            }
        }

        private void HandleServerInput()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                SendSampleMessage();
            }
        }

        private void HandleClientInput()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                SendSampleMessage();
            }
        }

        public void SendSampleMessage()
        {
            if (!IsSpawned || dispatcher == null) return;
            SampleMessage message = new SampleMessage { Id = ++messageCounter, Timestamp = Time.time };
            SendMessageForRole(message);
        }

        private void OnSampleMessageReceived(ulong senderClientId, SampleMessage message)
        {
            Debug.Log($"{(IsServer ? "[Server]" : $"[Client {NetworkManager.Singleton.LocalClientId}]")} Received SampleMessage ID: {message.Id}, Timestamp: {message.Timestamp} from Client: {senderClientId}");
            if (IsServer && dispatcher != null)
            {
                BounceToOtherClients(senderClientId, message);
            }
        }

        private void SendMessageForRole(SampleMessage message)
        {
            if (IsServer)
            {
                SendMessageAsServer(message);
            }
            else if (IsClient)
            {
                SendMessageAsClient(message);
            }
        }

        private void SendMessageAsServer(SampleMessage message)
        {
            Debug.Log($"[Server] Sending SampleMessage ID: {message.Id} to all clients...");
            List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            dispatcher.SendToClients(message, clientIds);
        }

        private void SendMessageAsClient(SampleMessage message)
        {
            Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Sending SampleMessage ID: {message.Id} to server...");
            dispatcher.SendToServer(message);
        }

        private void BounceToOtherClients(ulong senderClientId, SampleMessage message)
        {
            Debug.Log($"[Server] Bouncing message {message.Id} from {senderClientId} to everyone else.");
            List<ulong> otherClients = BuildOtherClientList(senderClientId);
            if (otherClients.Count > 0)
            {
                dispatcher.SendToClients(message, otherClients);
            }
        }

        private List<ulong> BuildOtherClientList(ulong senderClientId)
        {
            List<ulong> otherClients = new List<ulong>();
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId != senderClientId) otherClients.Add(clientId);
            }
            return otherClients;
        }

        private void CleanupDispatcher()
        {
            if (dispatcher == null) return;
            dispatcher.UnregisterHandler<SampleMessage>();
            DisposeDispatcher();
            dispatcher = null;
        }

        private void DisposeDispatcher()
        {
            if (dispatcher is System.IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

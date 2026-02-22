using System;
using System.Collections.Generic;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Contract for a service capable of routing strongly typed unmanaged structs over the network.
    /// </summary>
    public interface INetworkMessageDispatcher
    {
        /// <summary>
        /// Registers a handler for a specific unmanaged message type <typeparamref name="T"/>.
        /// </summary>
        void RegisterHandler<T>(Action<ulong, T> handler) where T : unmanaged;

        /// <summary>
        /// Unregisters a previously registered handler for the unmanaged message type <typeparamref name="T"/>.
        /// </summary>
        void UnregisterHandler<T>() where T : unmanaged;

        /// <summary>
        /// Sends an unmanaged message to the server. Can only be called from a client.
        /// </summary>
        void SendToServer<T>(T message) where T : unmanaged;

        /// <summary>
        /// Sends an unmanaged message from the server to a specific client. Can only be called from the server.
        /// </summary>
        void SendToClient<T>(T message, ulong clientId) where T : unmanaged;

        /// <summary>
        /// Sends an unmanaged message from the server to multiple clients. Can only be called from the server.
        /// </summary>
        void SendToClients<T>(T message, IReadOnlyList<ulong> clientIds) where T : unmanaged;
    }
}

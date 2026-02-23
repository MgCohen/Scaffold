using System;
using System.Collections.Generic;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Contract for routing strongly typed unmanaged structs over the network.
    /// Implementations can apply internal ordering and recovery for out-of-order payloads.
    /// </summary>
    public interface INetworkMessageDispatcher
    {
        void RegisterHandler<T>(Action<ulong, T> handler) where T : unmanaged;

        void UnregisterHandler<T>() where T : unmanaged;

        void SendToServer<T>(T message) where T : unmanaged;

        void SendToClient<T>(T message, ulong clientId) where T : unmanaged;

        void SendToClients<T>(T message, IReadOnlyList<ulong> clientIds) where T : unmanaged;
    }
}

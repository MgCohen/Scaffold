using System;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Transport boundary for command envelopes.
    /// </summary>
    public interface ICommandTransport
    {
        void AddReceiver(Action<CommandEnvelope> receiver);

        void RemoveReceiver(Action<CommandEnvelope> receiver);

        void Send(CommandEnvelope envelope);
    }
}

using System;

namespace Scaffold.Commands
{
    /// <summary>
    /// Transport boundary used by command service implementations.
    /// Implementations are expected to bridge command traffic to systems such as NetworkMessages.
    /// </summary>
    public interface ICommandTransport
    {
        void AddReceiver(Action<CommandTransportMessage> receiver);

        void RemoveReceiver(Action<CommandTransportMessage> receiver);

        void Send(CommandTransportMessage message);
    }
}

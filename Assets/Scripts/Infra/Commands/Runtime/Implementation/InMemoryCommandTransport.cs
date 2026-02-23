using System;
using System.Collections.Generic;

namespace Scaffold.Commands
{
    /// <summary>
    /// In-process command transport useful for local and test scenarios.
    /// </summary>
    public class InMemoryCommandTransport : ICommandTransport
    {
        private readonly object syncRoot = new object();
        private readonly List<Action<CommandTransportMessage>> receivers = new List<Action<CommandTransportMessage>>();

        public void AddReceiver(Action<CommandTransportMessage> receiver)
        {
            var hasReceiver = receiver != null;
            if (hasReceiver)
            {
                lock (syncRoot)
                {
                    var isRegistered = receivers.Contains(receiver);
                    if (!isRegistered)
                    {
                        receivers.Add(receiver);
                    }
                }
            }
        }

        public void RemoveReceiver(Action<CommandTransportMessage> receiver)
        {
            var hasReceiver = receiver != null;
            if (hasReceiver)
            {
                lock (syncRoot)
                {
                    receivers.Remove(receiver);
                }
            }
        }

        public void Send(CommandTransportMessage message)
        {
            var hasMessage = message != null;
            if (hasMessage)
            {
                var snapshot = CreateSnapshot();
                NotifyReceivers(snapshot, message);
            }
        }

        private List<Action<CommandTransportMessage>> CreateSnapshot()
        {
            List<Action<CommandTransportMessage>> snapshot;
            lock (syncRoot)
            {
                snapshot = new List<Action<CommandTransportMessage>>(receivers);
            }
            return snapshot;
        }

        private void NotifyReceivers(List<Action<CommandTransportMessage>> snapshot, CommandTransportMessage message)
        {
            foreach (var receiver in snapshot)
            {
                receiver.Invoke(message);
            }
        }
    }
}

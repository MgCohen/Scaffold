using System;
using System.Collections.Generic;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// In-process transport implementation for local command routing and tests.
    /// </summary>
    public class InMemoryCommandTransport : ICommandTransport
    {
        private readonly object syncRoot = new object();
        private readonly List<Action<CommandEnvelope>> receivers = new List<Action<CommandEnvelope>>();

        public void AddReceiver(Action<CommandEnvelope> receiver)
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

        public void RemoveReceiver(Action<CommandEnvelope> receiver)
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

        public void Send(CommandEnvelope envelope)
        {
            var hasEnvelope = envelope != null;
            if (hasEnvelope)
            {
                List<Action<CommandEnvelope>> snapshot;
                lock (syncRoot)
                {
                    snapshot = new List<Action<CommandEnvelope>>(receivers);
                }
                foreach (var receiver in snapshot)
                {
                    receiver.Invoke(envelope);
                }
            }
        }
    }
}

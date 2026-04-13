using System;

namespace Scaffold.Entities
{
    internal sealed class VariableSubscriptionToken : IDisposable
    {
        internal VariableSubscriptionToken(VariableNotifier notifier, Variable key, Action<VariableValue> adapter)
        {
            this.notifier = notifier;
            this.key = key;
            this.adapter = adapter;
        }

        private readonly VariableNotifier notifier;
        private readonly Variable key;
        private readonly Action<VariableValue> adapter;
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            notifier.Remove(key, adapter);
        }
    }
}

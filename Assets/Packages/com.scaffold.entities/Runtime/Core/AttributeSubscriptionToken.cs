using System;

namespace Scaffold.Entities
{
    internal sealed class AttributeSubscriptionToken : IDisposable
    {
        internal AttributeSubscriptionToken(AttributeNotifier notifier, Attribute attribute, Action<AttributeValue> adapter)
        {
            this.notifier = notifier;
            this.attribute = attribute;
            this.adapter = adapter;
        }

        private readonly AttributeNotifier notifier;
        private readonly Attribute attribute;
        private readonly Action<AttributeValue> adapter;
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            notifier.Remove(attribute, adapter);
        }
    }
}

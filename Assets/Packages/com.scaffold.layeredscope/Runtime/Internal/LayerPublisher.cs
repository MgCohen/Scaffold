using System;
using System.Collections.Generic;
using Scaffold.LayeredScope;
using VContainer;

namespace Scaffold.LayeredScope.Internal
{
    internal sealed class LayerPublisher : ILayerPublisher
    {
        public IReadOnlyList<Action<IContainerBuilder>> Deltas => deltas;

        private readonly List<Action<IContainerBuilder>> deltas = new();

        public void Publish<T>(T asset) where T : class
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            deltas.Add(b => b.RegisterInstance(asset));
        }

        public void Publish<TInterface, TImpl>(TImpl asset) where TImpl : class, TInterface
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            deltas.Add(b => b.RegisterInstance<TImpl, TInterface>(asset));
        }

        public void PublishMany<T>(IReadOnlyList<T> items) where T : class
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            deltas.Add(b => ApplyPublishManyToBuilder(b, items));
        }

        public void Apply(IContainerBuilder builder)
        {
            for (int i = 0; i < deltas.Count; i++)
            {
                deltas[i](builder);
            }
        }

        private void ApplyPublishManyToBuilder<T>(IContainerBuilder b, IReadOnlyList<T> items) where T : class
        {
            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item == null)
                {
                    throw new ArgumentException("List must not contain null entries.", nameof(items));
                }

                b.RegisterInstance(item);
            }

            b.RegisterInstance<IReadOnlyList<T>>(items);
        }
    }
}

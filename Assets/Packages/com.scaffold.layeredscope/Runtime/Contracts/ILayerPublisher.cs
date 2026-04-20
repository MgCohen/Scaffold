using System.Collections.Generic;

namespace Scaffold.LayeredScope
{
    public interface ILayerPublisher
    {
        void Publish<T>(T asset) where T : class;

        void Publish<TInterface, TImpl>(TImpl asset) where TImpl : class, TInterface;

        void PublishMany<T>(IReadOnlyList<T> items) where T : class;
    }
}

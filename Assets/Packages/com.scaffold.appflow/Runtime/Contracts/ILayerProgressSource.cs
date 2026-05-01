using System;

namespace Scaffold.AppFlow
{
    public interface ILayerProgressSource
    {
        float Progress { get; }

        event Action<float> ProgressChanged;
    }
}

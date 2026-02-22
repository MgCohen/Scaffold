using System;
using UnityEngine;

namespace Scaffold.Containers
{
    public interface IContainerAdapter
    {
        void Run(Transform root, Action<IContext> build);
    }
}

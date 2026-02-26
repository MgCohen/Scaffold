using System;
using UnityEngine;

namespace Scaffold.Containers
{
    internal interface IContainerScope : IDisposable
    {
        Transform Transform { get; }
        void BuildChild(Container container, Context childContext, Transform holder);
    }
}

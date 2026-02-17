using UnityEngine;

namespace Scaffold.LifeCycle
{
    public interface IDispose
    {
        public Awaitable Dispose();
    }
}
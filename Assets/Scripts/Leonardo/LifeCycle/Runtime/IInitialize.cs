using UnityEngine;

namespace Scaffold.LifeCycle.Shared
{
    public interface IInitialize
    {
        public Awaitable Initialize();
    }
}
using UnityEngine;

namespace Scaffold.LifeCycle.Shared
{
    public interface IExecute
    {
        public Awaitable Execute();
    }
}

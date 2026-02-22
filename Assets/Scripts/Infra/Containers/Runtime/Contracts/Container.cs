using UnityEngine;

namespace Scaffold.Containers
{
    public abstract class Container
    {
        public virtual void Build(IContainerRegistry registry, Transform holder)
        {

        }
    }
}

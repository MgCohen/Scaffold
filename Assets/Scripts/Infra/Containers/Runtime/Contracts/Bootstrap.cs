using UnityEngine;

namespace Scaffold.Containers
{
    public abstract class Bootstrap : MonoBehaviour
    {
        protected virtual IContainerAdapter GetAdapter()
        {
            return new VContainerAdapter();
        }

        private void Start()
        {
            GetAdapter().Run(transform, Build);
        }

        protected abstract void Build(IContext context);
    }
}

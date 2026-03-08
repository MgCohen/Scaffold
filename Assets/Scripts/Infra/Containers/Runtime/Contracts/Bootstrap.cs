using UnityEngine;

namespace Scaffold.Containers
{
    public abstract class Bootstrap : MonoBehaviour
    {
        private void Start()
        {
            IContainerAdapter adapter = GetAdapter();
            adapter.Run(transform, Build);
        }

        protected virtual IContainerAdapter GetAdapter()
        {
            return new VContainerAdapter();
        }

        protected abstract void Build(IContext context);
    }
}

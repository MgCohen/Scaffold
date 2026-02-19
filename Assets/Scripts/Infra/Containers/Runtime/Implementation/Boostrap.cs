using UnityEngine;

namespace Scaffold.Containers
{
    public abstract class Boostrap : MonoBehaviour
    {
        protected virtual IContainerAdapter GetAdapter() => new VContainerAdapter();

        private void Start()
        {
            GetAdapter().Run(transform, Build);
        }

        protected abstract void Build(IContext context);
    }
}

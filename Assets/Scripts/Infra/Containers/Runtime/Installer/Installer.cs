using UnityEngine;

namespace Scaffold.Containers
{
    public class Installer
    {
        public virtual void Install(IContainerBuilder builder, ContainerConfig config, Transform holder)
        {
        }
    }
}

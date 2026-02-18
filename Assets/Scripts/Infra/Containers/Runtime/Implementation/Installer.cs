using UnityEngine;

namespace Scaffold.Containers
{
    public abstract class Installer
    {
        public abstract void Install(IContainerBuilder builder, Transform holder);
    }
}

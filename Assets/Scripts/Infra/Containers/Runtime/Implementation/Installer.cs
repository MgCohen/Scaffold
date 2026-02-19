using UnityEngine;

namespace Scaffold.Containers
{
    public abstract class Installer
    {
        public abstract void Install(IContainerRegistry registry, Transform holder);
    }
}

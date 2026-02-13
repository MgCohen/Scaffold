using VContainer.Unity;
using UnityEngine;
using VContainer;
using System;
using System.Runtime.CompilerServices;

namespace Scaffold.Containers
{
    [Serializable]
    internal class InstallerWrapper<T> : IInstaller where T : UnityEngine.Object, IInstaller
    {
        public InstallerWrapper()
        {

        }

        [SerializeField] private T installer;

        public void Install(IContainerBuilder builder)
        {
            installer.Install(builder);
        }
    }
}

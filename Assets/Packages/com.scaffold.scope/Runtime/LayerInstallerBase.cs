using System;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Scope
{
    public abstract class LayerInstallerBase
    {
        protected abstract void Install(IContainerBuilder builder);

        protected static void BuildInstaller(IContainerBuilder builder, IInstaller installer)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (installer == null)
            {
                throw new ArgumentNullException(nameof(installer));
            }

            installer.Install(builder);
        }
    }
}

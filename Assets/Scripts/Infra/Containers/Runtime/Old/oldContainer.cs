using System.Collections.Generic;
using VContainer.Unity;
using UnityEngine;
using VContainer;
using System.Threading.Tasks;
using System;

namespace Scaffold.Containers
{
    public class oldContainer : LifetimeScope
    {
        [SerializeField] private Action<IObjectResolver> containerReady = delegate { };
        [SerializeField] private ContainerState state;
        [SerializeField] private oldContainer parentContainer;
        [SerializeField, SerializeReference] private List<IInstaller> installers = new List<IInstaller>();

        protected override void Awake()
        {
            if (parentContainer != null)
            {
                parentReference = new ParentReference() { Object = parentContainer };
                parentContainer.OnContainerReady((o) => base.Awake());
            }
            else
            {
                base.Awake();
            }
        }

        public void OnContainerReady(Action<IObjectResolver> callback)
        {
            if (state is ContainerState.Open)
            {
                callback?.Invoke(Container);
            }
            else
            {
                containerReady += callback;
            }
        }

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            SetState(builder);
            BuildInstallers(builder);
        }

        private void SetState(IContainerBuilder builder)
        {
            state = ContainerState.Initializing;
            builder.RegisterBuildCallback(o =>
            {
                state = ContainerState.Open;
                containerReady?.Invoke(o);
            });
        }

        private void BuildInstallers(IContainerBuilder builder)
        {
            foreach (var installer in installers)
            {
                installer.Install(builder);
            }
        }
    }
}

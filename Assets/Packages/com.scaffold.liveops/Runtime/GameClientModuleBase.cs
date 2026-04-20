using System;
using System.Threading;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Scaffold.LayeredScope;
using VContainer;

namespace Scaffold.LiveOps
{
    public abstract class GameClientModuleBase<T> : IGameClientModule, IAsyncInitializable where T : class, IGameModuleData
    {
        protected GameClientModuleBase(IObjectResolver resolver)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public virtual string Key => typeof(T).Name;

        protected T data;

        private readonly IObjectResolver resolver;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ILiveOpsService liveOps = resolver.Resolve<ILiveOpsService>();
            T moduleData = liveOps.GetModuleData<T>();
            data = moduleData;
            return OnInitializedAsync(moduleData);
        }

        protected virtual Task OnInitializedAsync(T moduleData)
        {
            return Task.CompletedTask;
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Scaffold.AppFlow;

namespace Scaffold.LiveOps
{
    public abstract class GameClientModuleBase<T> : IGameClientModule, IAsyncInitializable where T : class, IGameModuleData
    {
        protected GameClientModuleBase(ILiveOpsService liveOps)
        {
            this.liveOps = liveOps ?? throw new ArgumentNullException(nameof(liveOps));
        }

        public virtual string Key => typeof(T).Name;

        protected T data;

        protected readonly ILiveOpsService liveOps;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

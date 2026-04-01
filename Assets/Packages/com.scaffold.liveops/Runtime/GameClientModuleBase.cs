using System;
using System.Threading;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Scaffold.Scope.Contracts;
using VContainer;

namespace Scaffold.LiveOps
{
    /// <summary>
    /// Base class for client game modules with strongly typed <see cref="IGameModuleData"/>.
    /// Implements <see cref="IAsyncLayerInitializable"/> to assign the <c>data</c> field from <see cref="ILiveOpsService.GetModuleData{T}"/>.
    /// Bootstrap layer ordering should run <c>LiveOpsService</c> before modules that depend on aggregated <c>GameData</c>.
    /// </summary>
    /// <typeparam name="T">Concrete module data type from shared DTOs.</typeparam>
    public abstract class GameClientModuleBase<T> : IGameClientModule, IAsyncLayerInitializable where T : class, IGameModuleData
    {
        public virtual string Key => typeof(T).Name;

        protected T data;

        public Task InitializeAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

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

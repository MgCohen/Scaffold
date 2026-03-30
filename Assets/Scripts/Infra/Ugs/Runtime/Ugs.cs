using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Scope.Contracts;
using Unity.Services.Authentication;
using Unity.Services.Core;
using VContainer;

namespace Scaffold.Ugs
{
    public sealed class Ugs : IAsyncLayerInitializable
    {
        public async Task InitializeAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            await EnsureInitializedAsync(cancellationToken);
        }

        internal async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (UnityServices.State is ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
    }
}

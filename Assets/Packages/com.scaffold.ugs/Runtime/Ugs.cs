using System.Threading;
using System.Threading.Tasks;
using Scaffold.AppFlow;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace Scaffold.Ugs
{
    public sealed class Ugs : IAsyncInitializable
    {
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return EnsureInitializedAsync(cancellationToken);
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

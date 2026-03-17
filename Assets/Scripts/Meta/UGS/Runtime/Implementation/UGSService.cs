using System.Threading.Tasks;
using Scaffold.LifeCycle;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;

namespace Scaffold.UGS
{
    public class UGSService : IController
    {
        public async Task Initialize()
        {
            await UnityServices.InitializeAsync();
            await SignUpAnonymouslyAsync();
        }

        public Task Dispose()
        {
            return Task.CompletedTask;
        }
        
        async Task SignUpAnonymouslyAsync()
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Sign in anonymously succeeded!");

                // Shows how to get the playerID
                Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");

            }
            catch (AuthenticationException ex)
            {
                // Compare error code to AuthenticationErrorCodes
                // Notify the player with the proper error message
                Debug.LogException(ex);
            }
            catch (RequestFailedException ex)
            {
                // Compare error code to CommonErrorCodes
                // Notify the player with the proper error message
                Debug.LogException(ex);
            }
        }
    }
}
using System;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using Unity.Services.CloudCode.Core;

namespace GameModule.AccessKey
{
    /// <summary>
    /// Provides extension utilities for validating execution contexts against game state authentication tokens.
    /// </summary>
    public static class AccessKey
    {
        /// <summary>
        /// Retrieves the authorization token mapped for Unity services natively.
        /// </summary>
        /// <param name="context">The active session context instance.</param>
        /// <param name="gameState">The current remote game tracking configuration.</param>
        /// <returns>A string payload containing the authorized token explicitly.</returns>
        public static async Task<string> GetUnityAuth(this IExecutionContext context, GameState gameState)
        {
            return await gameState.GetUnityAuth(context);
        }

        /// <summary>
        /// Matches the provided token against the server expected authentication dynamically.
        /// </summary>
        /// <param name="context">The session executing the request.</param>
        /// <param name="gameState">The target configuration parameters container.</param>
        /// <param name="auth">The token passed explicitly for verification.</param>
        /// <returns>True if the provided credential string matches successfully.</returns>
        public static async Task<bool> GetValidAuth(this IExecutionContext context, GameState gameState, string auth)
        {
            if (string.IsNullOrEmpty(auth))
            {
                return false;
            }

            string unityAuth = await GetUnityAuth(context, gameState);
            return unityAuth == auth;
        }

        /// <summary>
        /// Asserts the request authentication string and throws an exception otherwise natively.
        /// </summary>
        /// <param name="context">The calling logical interface.</param>
        /// <param name="gameState">The game state fetching data parameters safely.</param>
        /// <param name="auth">The input token verifying access correctly.</param>
        /// <returns>True seamlessly validating execution flows.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when token validation actively fails.</exception>
        public static async Task<bool> ValidateAuth(this IExecutionContext context, GameState gameState, string auth)
        {
            bool valid = await context.GetValidAuth(gameState, auth);
            return valid ? true : throw new UnauthorizedAccessException("Not Authorized");
        }
    }
}
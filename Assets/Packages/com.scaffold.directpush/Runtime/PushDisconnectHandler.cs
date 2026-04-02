using System;
using System.Threading;
using System.Threading.Tasks;
using GameModuleDTO.Modules.DirectPush;
using Scaffold.Scope.Contracts;
using UnityEngine;
using VContainer;

namespace Scaffold.DirectPush
{
    /// <summary>
    /// Handles multi-session disconnect enforcement via push notifications.
    /// Subscribes to player and project disconnect messages and terminates the application when received.
    /// </summary>
    public sealed class PushDisconnectHandler : IAsyncLayerInitializable, IDisposable
    {
        private readonly PushSubscriptionService _subscriptionService;
        private readonly DirectPushClient _pushClient;

        public PushDisconnectHandler(PushSubscriptionService subscriptionService, DirectPushClient pushClient)
        {
            _subscriptionService = subscriptionService;
            _pushClient = pushClient;
        }

        /// <summary>
        /// Registers push listeners for disconnect messages.
        /// Must run before <see cref="PushSubscriptionService"/> initializes its subscriptions.
        /// </summary>
        public Task InitializeAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _subscriptionService.SubscribeToPlayerMessage(
                PushToPlayerKeys.PushDisconnectMultiplePlayerAccounts,
                OnDisconnectReceived);

            _subscriptionService.SubscribeToProjectMessage(
                PushToProjectKeys.Disconnect,
                OnDisconnectReceived);

            Debug.Log("[PushDisconnectHandler] Subscribed to disconnect push messages.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends a self-targeted push to disconnect the current player's other sessions.
        /// </summary>
        public Task SendDisconnectSelfAsync(CancellationToken cancellationToken = default)
        {
            Debug.Log("[PushDisconnectHandler] Sending self disconnect push.");
            return _pushClient.SendSelfPushAsync(
                "disconnect",
                PushToPlayerKeys.PushDisconnectMultiplePlayerAccounts,
                cancellationToken);
        }

        /// <summary>
        /// Broadcasts a project-wide disconnect push to all connected players.
        /// Requires a valid AccessKey GUID.
        /// </summary>
        /// <param name="accessKeyGuid">The server AccessKey GUID for validation.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public Task SendDisconnectProjectAsync(string accessKeyGuid, CancellationToken cancellationToken = default)
        {
            Debug.Log("[PushDisconnectHandler] Sending project disconnect push.");
            return _pushClient.SendProjectPushAsync(
                "disconnect",
                PushToProjectKeys.Disconnect,
                accessKeyGuid,
                cancellationToken);
        }

        public void Dispose()
        {
        }

        private void OnDisconnectReceived()
        {
            Debug.LogWarning("[PushDisconnectHandler] Disconnect push received — quitting application.");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

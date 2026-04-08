using System;
using System.Threading;
using System.Threading.Tasks;
using GameModuleDTO.Modules.DirectPush;
using Scaffold.Scope.Contracts;
using UnityEngine;
using VContainer;

namespace Scaffold.DirectPush
{
    public sealed class PushDisconnectHandler : IAsyncLayerInitializable, IDisposable
    {
        public PushDisconnectHandler(PushSubscriptionService subscriptionService, DirectPushClient pushClient)
        {
            this.subscriptionService = subscriptionService;
            this.pushClient = pushClient;
        }

        private readonly PushSubscriptionService subscriptionService;
        private readonly DirectPushClient pushClient;

        public Task InitializeAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            subscriptionService.SubscribeToPlayerMessage(
                PushToPlayerKeys.PushDisconnectMultiplePlayerAccounts,
                OnDisconnectReceived);

            subscriptionService.SubscribeToProjectMessage(
                PushToProjectKeys.Disconnect,
                OnDisconnectReceived);

            Debug.Log("[PushDisconnectHandler] Subscribed to disconnect push messages.");
            return Task.CompletedTask;
        }

        public Task SendDisconnectSelfAsync(CancellationToken cancellationToken = default)
        {
            Debug.Log("[PushDisconnectHandler] Sending self disconnect push.");
            return pushClient.SendSelfPushAsync(
                "disconnect",
                PushToPlayerKeys.PushDisconnectMultiplePlayerAccounts,
                cancellationToken);
        }

        public Task SendDisconnectProjectAsync(string accessKeyGuid, CancellationToken cancellationToken = default)
        {
            Debug.Log("[PushDisconnectHandler] Sending project disconnect push.");
            return pushClient.SendProjectPushAsync(
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Scaffold.Scope.Contracts;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using UnityEngine;
using VContainer;

namespace Scaffold.DirectPush
{
    public sealed class PushSubscriptionService : IAsyncLayerInitializable, IDisposable
    {
        private readonly Dictionary<string, List<Action>> playerHandlers = new Dictionary<string, List<Action>>();
        private readonly Dictionary<string, List<Action>> projectHandlers = new Dictionary<string, List<Action>>();

        public void SubscribeToPlayerMessage(string messageType, Action handler)
        {
            if (!playerHandlers.TryGetValue(messageType, out List<Action> handlers))
            {
                handlers = new List<Action>();
                playerHandlers[messageType] = handlers;
            }

            handlers.Add(handler);
        }

        public void SubscribeToProjectMessage(string messageType, Action handler)
        {
            if (!projectHandlers.TryGetValue(messageType, out List<Action> handlers))
            {
                handlers = new List<Action>();
                projectHandlers[messageType] = handlers;
            }

            handlers.Add(handler);
        }

        public async Task InitializeAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SubscribeToPlayerMessages();
            await SubscribeToProjectMessages();
        }

        public void Dispose()
        {
            playerHandlers.Clear();
            projectHandlers.Clear();
        }

        private Task SubscribeToPlayerMessages()
        {
            SubscriptionEventCallbacks callbacks = BuildPlayerCallbacks();
            return CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
        }

        private Task SubscribeToProjectMessages()
        {
            SubscriptionEventCallbacks callbacks = BuildProjectCallbacks();
            return CloudCodeService.Instance.SubscribeToProjectMessagesAsync(callbacks);
        }

        private SubscriptionEventCallbacks BuildPlayerCallbacks()
        {
            SubscriptionEventCallbacks callbacks = new SubscriptionEventCallbacks();
            callbacks.MessageReceived += @event => { Debug.Log($"[DirectPush] Player message received — type: {@event.MessageType}"); DispatchHandlers(@event.MessageType, playerHandlers); };
            callbacks.ConnectionStateChanged += @event => { Debug.Log($"[DirectPush] Player subscription state changed: {@event}"); };
            callbacks.Kicked += () => { Debug.LogWarning("[DirectPush] Player subscription kicked."); };
            callbacks.Error += @event => { Debug.LogError("[DirectPush] Player subscription error: " + JsonConvert.SerializeObject(@event, Formatting.Indented)); };
            return callbacks;
        }

        private SubscriptionEventCallbacks BuildProjectCallbacks()
        {
            SubscriptionEventCallbacks callbacks = new SubscriptionEventCallbacks();
            callbacks.MessageReceived += @event => { Debug.Log($"[DirectPush] Project message received — type: {@event.MessageType}"); DispatchHandlers(@event.MessageType, projectHandlers); };
            callbacks.ConnectionStateChanged += @event => { Debug.Log($"[DirectPush] Project subscription state changed: {@event}"); };
            callbacks.Kicked += () => { Debug.LogWarning("[DirectPush] Project subscription kicked."); };
            callbacks.Error += @event => { Debug.LogError("[DirectPush] Project subscription error: " + JsonConvert.SerializeObject(@event, Formatting.Indented)); };
            return callbacks;
        }

        private void DispatchHandlers(string messageType, Dictionary<string, List<Action>> registry)
        {
            if (registry.TryGetValue(messageType, out List<Action> handlers))
            {
                foreach (Action handler in handlers)
                {
                    handler.Invoke();
                }
            }
        }
    }
}

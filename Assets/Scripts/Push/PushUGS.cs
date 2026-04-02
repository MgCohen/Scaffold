using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using UnityEngine;

namespace Data2073.Shared.UGS
{
    using Action = System.Action;

    public class PushUGS : SerializedMonoBehaviour
    {
        [SerializeField]
        private Dictionary<string, List<Action>> playerActions = new Dictionary<string, List<Action>>();
        [SerializeField]
        private Dictionary<string, List<Action>> projectActions = new Dictionary<string, List<Action>>();

        public void SubscribeToPlayerAction(string key, Action action)
        {
            Debug.LogClient(key, "PushUGS.SubscribeToPlayerAction");
            if (playerActions.ContainsKey(key))
            {
                playerActions[key].Add(action);
            }
            else
            {
                playerActions.Add(key, new List<Action>() { action });
            }
        }
        
        public void SubscribeToProjectAction(string key, Action action)
        {
            Debug.LogClient(key, "PushUGS.SubscribeToProjectAction");
            if (projectActions.ContainsKey(key))
            {
                projectActions[key].Add(action);
            }
            else
            {
                projectActions.Add(key, new List<Action>() { action });
            }
        }

        public void TryExecuteActions(string key, Dictionary<string, List<Action>> actions)
        {
            //Debug.LogClient(key, "PushUGS.TryExecuteActions");
            if (actions.TryGetValue(key, out List<Action> outActions))
            {
                foreach (Action action in outActions)
                {
                    action.Invoke();
                }
            }
        }
        
        public async Task SubscribeEvents()
        {
            Debug.LogClient("PushUGS.SubscribeEvents");
            await SubscribeToPlayerMessages();
            await SubscribeToProjectMessages();
        }
        
        // This method creates a subscription to player messages and logs out the messages received,
        // the state changes of the connection, when the player is kicked and when an error occurs.
        private Task SubscribeToPlayerMessages()
        {
            // Register callbacks, which are triggered when a player message is received
            SubscriptionEventCallbacks callbacks = new SubscriptionEventCallbacks();
            callbacks.MessageReceived += @event =>
            {
                //Debug.LogClient(DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"), "SubscribeToPlayerMessages");
                Debug.LogClient($"Got player subscription Message: {JsonConvert.SerializeObject(@event, Formatting.Indented)}", "SubscribeToPlayerMessages");
                TryExecuteActions(@event.MessageType, playerActions);
            };
            callbacks.ConnectionStateChanged += @event =>
            {
                Debug.LogClient($"Got player subscription ConnectionStateChanged: {@event}", "SubscribeToPlayerMessages");
            };
            callbacks.Kicked += () =>
            {
                Debug.LogClient($"Got player subscription Kicked", "SubscribeToPlayerMessages");
            };
            callbacks.Error += @event =>
            {
                Debug.LogClient($"Got player subscription Error: {JsonConvert.SerializeObject(@event, Formatting.Indented)}", "SubscribeToPlayerMessages");
            };
            return CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
        }
        
        // This method creates a subscription to project messages and logs out the messages received,
        // the state changes of the connection, when the player is kicked and when an error occurs.
        private Task SubscribeToProjectMessages()
        {
            SubscriptionEventCallbacks callbacks = new SubscriptionEventCallbacks();
            callbacks.MessageReceived += @event =>
            {
                //Debug.LogClient(DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"), "SubscribeToProjectMessages");
                Debug.LogClient($"Got project subscription Message: {JsonConvert.SerializeObject(@event, Formatting.Indented)}", "SubscribeToProjectMessages");
                TryExecuteActions(@event.MessageType, projectActions);
            };
            callbacks.ConnectionStateChanged += @event =>
            {
                Debug.LogClient($"Got project subscription ConnectionStateChanged: {@event}", "SubscribeToProjectMessages");
            };
            callbacks.Kicked += () =>
            {
                Debug.LogClient($"Got project subscription Kicked", "SubscribeToProjectMessages");
            };
            callbacks.Error += @event =>
            {
                Debug.LogClient($"Got project subscription Error: {JsonConvert.SerializeObject(@event, Formatting.Indented)}", "SubscribeToProjectMessages");
            };
            return CloudCodeService.Instance.SubscribeToProjectMessagesAsync(callbacks);
        }
    }
}
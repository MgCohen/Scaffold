using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Data2073.Client.User;
using Data2073.Shared;
using Data2073.Shared.GameModules;
using Data2073.Shared.UGS;
using GameModuleDTO.Keys;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Data2073.Client
{
    public class ClientPushController : MonoBehaviour, IController
    {
        [Inject]
        [SerializeField]
        private GameModuleBindings gameModuleBindings;
        [Inject]
        [SerializeField]
        private AnalyticsController analyticsController;
        [Inject]
        [SerializeField]
        private PushUGS pushUGS;
        [Inject]
        [SerializeField]
        private UserController userController;

        public UniTask Initialize()
        {
            pushUGS.SubscribeToPlayerAction(PushToPlayerKeys.PushDisconnectMultiplePlayerAccounts, OnDisconnectMultipleAccounts);
            pushUGS.SubscribeToProjectAction(PushToProjectKeys.Disconnect, OnDisconnectMultipleAccounts);
            return UniTask.CompletedTask;
        }

        public UniTask Dispose()
        {
            throw new System.NotImplementedException();
        }
        
        [Button]
        public async Task DisconnectMultipleAccounts()
        {
            Debug.LogClient("DisconnectMultipleAccounts");
            await gameModuleBindings.SendSelfPlayerDisconnectMultipleAccounts();
        }
        
        [Button]
        public async Task DisconnectAllAccounts()
        {
            Debug.LogClient("DisconnectAllAccounts");
            await gameModuleBindings.SendProjectDisconnect();
        }
        
        [Button]
        private void OnDisconnectMultipleAccounts()
        {
            Debug.LogClientWarning($"PlayerId: {userController.Data.playerId}", "OnDisconnectMultipleAccounts");
            analyticsController.TriedToLoginInMultipleAccounts(userController.Data.playerId);
            Utilities.Quit();
        }
    }
}
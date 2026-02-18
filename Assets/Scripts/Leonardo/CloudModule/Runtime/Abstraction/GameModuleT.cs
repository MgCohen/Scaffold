using System.Linq;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;
using UnityEngine;
using VContainer;

namespace Scaffold.CloudModules.Shared
{
    public abstract class GameModuleT<T> : MonoBehaviour, IGameModule where T : IGameModuleData
    {
        [Inject]
        [SerializeField]
        protected ICloudCodeService cloudCodeService;

        [SerializeField]
        private T data;
        public T Data
        {
            get { return data; }
            protected set
            {
                data = value;
            }
        }

        public IGameModuleData DataModule
        {
            get { return data; }
        }

        public async Awaitable Initialize(GameData gameModule)
        {
            GameDebug.Log($"{GetType().Name}, Data: {typeof(T).Name}, null: {Data == null}", "Initializing GameModule");
            T moduleData = gameModule.GetModuleData<T>();
            UpdateData(moduleData);
            await OnInitialize(moduleData);
        }

        protected abstract Awaitable OnInitialize(T gameModuleData);

        public virtual void UpdateData(T gameModuleData)
        {
            Data = gameModuleData;
            OnUpdateData(Data);
        }
        
        protected abstract Awaitable OnUpdateData(T gameModuleData);
        
        public virtual void UpdateData(IGameModuleData gameModuleData)
        {
            Data = (T)gameModuleData;
            OnUpdateData(Data);
        }

        public async Awaitable FetchModuleData()
        {
            GameDataResponse response = await cloudCodeService.CallEndpointAsync(new GameDataRequest(GameModuleAuthKey.guid, data.Key));
            if (response.GameData == null || response.GameData.modulesData.Any())
            {
                return;
            }
            
            foreach (IGameModuleData moduleData in response.GameData.modulesData)
            {
                IGameModule matchingModule = cloudCodeService.Modules.FirstOrDefault(m => m.DataModule?.GetType() == moduleData.GetType());
                matchingModule?.UpdateData(moduleData);
            }
        }
    }
}
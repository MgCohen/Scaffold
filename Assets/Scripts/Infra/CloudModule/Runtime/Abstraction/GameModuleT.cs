using GameModuleDTO.GameModule;
using Scaffold.Logging;
using UnityEngine;
using VContainer;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// Serves as the base abstract class for all specialized game modules that require data fetching and initialization.
    /// The main goal is to provide a generic wrapper capable of managing strongly-typed module data.
    /// It is used throughout the CloudModule layer to streamline the creation and data synchronization of individual backend features.
    /// </summary>
    public abstract class GameModuleT<T> : MonoBehaviour, IGameModule where T : IGameModuleData
    {
        [Inject]
        [SerializeField]
        protected ICloudCodeService _cloudCodeService;
        
        [Inject]
        [SerializeField]
        protected GameModulesController _gameModulesController;

        [SerializeField]
        private T _data;
        
        public T Data
        {
            get { return _data; }
            protected set
            {
                _data = value;
            }
        }

        public IGameModuleData DataModule
        {
            get { return _data; }
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

        public async Awaitable<T> FetchModuleData()
        {
            await _gameModulesController.FetchModuleData(_data.Key);
            return Data;
        }
    }
}
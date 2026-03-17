using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Scaffold.CloudGateway;
using Scaffold.Logging;
using UnityEngine;
using VContainer;

namespace Scaffold.GameModules
{
    /// <summary>
    /// Serves as the base abstract class for all specialized game modules that require data fetching and initialization.
    /// The main goal is to provide a generic wrapper capable of managing strongly-typed module data.
    /// It is used throughout the CloudModule layer to streamline the creation and data synchronization of individual backend features.
    /// </summary>
    public abstract class GameModule<T> : MonoBehaviour, IGameModule where T : IGameModuleData
    {
        [Inject]
        [SerializeField]
        protected ICloudService cloudService;

        [Inject]
        [SerializeField]
        protected IGameModulesService cloudGatewayService;

        [SerializeField]
        private T data;

        public T Data
        {
            get { return data; }
            protected set { data = value; }
        }

        public IGameModuleData DataModule
        {
            get { return data; }
        }

        public async Task Initialize(GameData gameModule)
        {
            GameDebug.Log($"{GetType().Name}, Data: {typeof(T).Name}, null: {Data == null}", "Initializing GameModule");
            T moduleData = gameModule.GetModuleData<T>();
            UpdateData(moduleData);
            await OnInitialize(moduleData);
        }

        protected abstract Task OnInitialize(T gameModuleData);

        public virtual void UpdateData(T gameModuleData)
        {
            Data = gameModuleData;
            OnUpdateData(Data);
        }

        protected abstract Task OnUpdateData(T gameModuleData);

        public virtual void UpdateData(IGameModuleData gameModuleData)
        {
            Data = (T)gameModuleData;
            OnUpdateData(Data);
        }

        public async Task<T> FetchModuleData()
        {
            await cloudGatewayService.FetchModuleData(data.Key);
            return Data;
        }
    }
}
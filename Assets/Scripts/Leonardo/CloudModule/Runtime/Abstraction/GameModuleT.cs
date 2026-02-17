using System.Linq;
using GameModuleDTO.GameModule;
using GameModuleDTO.Response;
using Scaffold.Logging;
using UnityEngine;
using Utility.List;
using VContainer;

namespace Scaffold.CloudModules.Shared
{
    public abstract class GameModuleT<T> : MonoBehaviour, IGameModule where T : IGameModuleData
    {
        protected string Guid
        {
            get
            {
                return GameModuleAuthKey.guid;
            }
        }

        [Inject]
        [SerializeField]
        protected ICloudModuleBinding moduleBinding;

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

        public async Awaitable Initialize(GameModuleDTO.GameModule.GameData gameModule)
        {
            Data = gameModule.GetModuleData<T>();
            GameDebug.Log($"Initializing GameModule: {GetType().Name}, Data: {typeof(T).Name}, null: {Data == null}");
            await OnInitialize();
        }

        protected abstract Awaitable OnInitialize();
        
        public virtual bool UpdateModulesFromResponse(Response response)
        {
            if (response == null)
            {
                GameDebug.Log($"Null response from GameModule: {typeof(T).Name}");
                return false;
            }
            
            if (moduleBinding.Modules.IsNullOrEmpty())
            {
                GameDebug.LogWarning(moduleBinding.Modules.IsNullOrEmpty() ?
                    "GameModuleBindings.Modules is null or empty." :
                    "Response.modules is null or empty.", "GameModule");
                return false;
            }
            
            GameDebug.Log($"Updating modules from response. Response status: {response.status}, Message: {response.message}");

            foreach (IGameModuleData moduleData in response.modules)
            {
                GameDebug.Log($"Updating module: {moduleData.Key} of type: {moduleData.GetType().Name}");
                IGameModule matchingModule = moduleBinding.Modules.FirstOrDefault(m => m.DataModule?.GetType() == moduleData.GetType());
                matchingModule?.UpdateData(moduleData);
            }
            return true;
        }

        public virtual void UpdateData(IGameModuleData gameModuleData)
        {
            Data = (T)gameModuleData;
        }

        public virtual void UpdateData(T gameModuleData)
        {
            Data = gameModuleData;
        }
    }
}
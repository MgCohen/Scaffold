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
        /// <summary>
        /// The injected cloud code service dependency.
        /// The main goal is to maintain a reference to the backend communication layer.
        /// It is used to make RPC calls seamlessly from within the module.
        /// </summary>
        [Inject]
        [SerializeField]
        protected ICloudCodeService _cloudCodeService;

        /// <summary>
        /// The injected game modules controller managing orchestrations.
        /// The main goal is to communicate with sibling modules or trigger global data fetching.
        /// It is used when this module needs to coordinate with higher-level systems.
        /// </summary>
        [Inject]
        [SerializeField]
        protected GameModulesController _gameModulesController;

        /// <summary>
        /// The typed data state corresponding to this module.
        /// The main goal is to cache the remote backend data locally.
        /// It is used as the current snapshot of the module's authoritative state.
        /// </summary>
        [SerializeField]
        private T _data;

        /// <summary>
        /// Gets or protectedly sets the strongly typed module data.
        /// The main goal is to encapsulate reading and writing to the module's state.
        /// It is used directly by inheritors to access their customized structure.
        /// </summary>
        public T Data
        {
            get { return _data; }
            protected set
            {
                _data = value;
            }
        }

        /// <summary>
        /// Gets the abstracted base interface representation of the module's data.
        /// The main goal is to fulfill the <see cref="IGameModule"/> contract.
        /// It is used by anonymous handlers iterating over disparate module lists.
        /// </summary>
        public IGameModuleData DataModule
        {
            get { return _data; }
        }

        /// <summary>
        /// Initializes the module by parsing typed data from a generic payload.
        /// The main goal is to extract our target slice of data and trigger the user override.
        /// It is used during the <see cref="GameModulesController"/> bootstrap sequence.
        /// </summary>
        public async Awaitable Initialize(GameData gameModule)
        {
            GameDebug.Log($"{GetType().Name}, Data: {typeof(T).Name}, null: {Data == null}", "Initializing GameModule");
            T moduleData = gameModule.GetModuleData<T>();
            UpdateData(moduleData);
            await OnInitialize(moduleData);
        }

        /// <summary>
        /// Hook for child classes to perform distinct async setup steps.
        /// The main goal is to provide a clean injection point for initialization logic.
        /// It is used to safely boot external presenters or internal systems.
        /// </summary>
        protected abstract Awaitable OnInitialize(T gameModuleData);

        /// <summary>
        /// Updates the internal strongly typed data struct.
        /// The main goal is to set the new state explicitly and trigger localized changes.
        /// It is used after fetching new info over the network.
        /// </summary>
        public virtual void UpdateData(T gameModuleData)
        {
            Data = gameModuleData;
            OnUpdateData(Data);
        }

        /// <summary>
        /// Hook for child classes to react defensively to incoming state mutations.
        /// The main goal is to enable dynamic refresh logic for module visuals or state.
        /// It is used whenever a network sync occurs for this module tier.
        /// </summary>
        protected abstract Awaitable OnUpdateData(T gameModuleData);

        /// <summary>
        /// Performs an unchecked cast to update the module's backing abstractions.
        /// The main goal is to comply with the base GameModule updating interface.
        /// It is used broadly by untyped orchestrators refreshing system subsets.
        /// </summary>
        public virtual void UpdateData(IGameModuleData gameModuleData)
        {
            Data = (T)gameModuleData;
            OnUpdateData(Data);
        }

        /// <summary>
        /// Requests a direct fresh fetch of this module's data from the backend.
        /// The main goal is to provide a helper to easily pull latest states sequentially.
        /// It is used for localized module data invalidation pipelines.
        /// </summary>
        public async Awaitable<T> FetchModuleData()
        {
            await _gameModulesController.FetchModuleData(_data.Key);
            return Data;
        }
    }
}
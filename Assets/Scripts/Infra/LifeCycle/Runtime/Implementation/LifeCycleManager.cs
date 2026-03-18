using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Scaffold.LifeCycle
{
    /// <summary>
    /// Coordinates the asynchronous initialization of all components implementing IInitialize.
    /// The main goal is to ensure a predictable setup sequence for system controllers after their dependencies are injected.
    /// It is used by the bootstrap layer to trigger the initialization logic of various game modules.
    /// </summary>
    public class LifeCycleManager : IInitializable
    {
        private readonly IEnumerable<IController> controllers;

        /// <summary>
        /// Initializes a new instance of the LifeCycleManager class with a collection of initializable components.
        /// </summary>
        /// <param name="initializables">The collection of components that require initialization.</param>
        public LifeCycleManager(IEnumerable<IController> iControllers)
        {
            this.controllers = iControllers;
        }

        /// <summary>
        /// Triggers the initialization of all registered components.
        /// The main goal is to await each component's setup while logging progress to the console.
        /// </summary>
        public async void Initialize()
        {
            foreach (IController controller in controllers)
            {
                string type = controller.GetType().Name;
                Debug.Log($"[LifeCycleManager] Initializing {type}...");
                await controller.Initialize();
                Debug.Log($"[LifeCycleManager] {type} initialized.");
            }
        }
    }
}

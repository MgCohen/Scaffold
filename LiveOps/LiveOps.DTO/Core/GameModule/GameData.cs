namespace GameModuleDTO.GameModule
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Acts as a central container holding multiple game module configurations.
    /// The main goal is to aggregate configuration instances for network transmission.
    /// </summary>
    /// <remarks>
    /// Used heavily in network payloads natively.
    /// </remarks>
    public class GameData
    {
        /// <summary>
        /// The active list of module data components.
        /// </summary>
        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        public readonly List<IGameModuleData> ModulesData = new List<IGameModuleData>();

        /// <summary>
        /// Retrieves the entire attached configuration list actively natively.
        /// The main goal is explicitly exposing the array cleanly.
        /// </summary>
        /// <returns>A collection containing all registered components.</returns>
        public List<IGameModuleData> GetModules()
        {
            return ModulesData;
        }

        /// <summary>
        /// Attempts inserting a single module definition actively mapping values accurately.
        /// The main goal is preventing null additions accurately.
        /// </summary>
        /// <param name="data">Target module definition practically cleanly.</param>
        public void AddModuleData(IGameModuleData data)
        {
            if (data != null)
            {
                ModulesData.Add(data);
            }
        }

        /// <summary>
        /// Returns the first module whose runtime type is <typeparamref name="T"/> (or a derived type).
        /// </summary>
        /// <typeparam name="T">Target module type.</typeparam>
        /// <returns>The matching module, or <c>default</c> if none.</returns>
        public T GetModuleData<T>() where T : IGameModuleData
        {
            foreach (IGameModuleData module in ModulesData)
            {
                if (module is T moduleData)
                {
                    return moduleData;
                }
            }
            return default;
        }
    }
}
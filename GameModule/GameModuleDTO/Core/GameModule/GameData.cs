namespace GameModuleDTO.GameModule
{
    using System.Collections.Generic;

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
        /// Appends multiple configuration records to the active list systematically.
        /// The main goal is easily appending lists seamlessly.
        /// </summary>
        /// <param name="value">A list of modules to append.</param>
        public void AddModules(List<IGameModuleData> value)
        {
            foreach (IGameModuleData gameData in value)
            {
                AddModuleData(gameData);
            }
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
        /// Searches logically resolving generic configuration models seamlessly.
        /// The main goal is reliably returning expected typed models gracefully.
        /// </summary>
        /// <typeparam name="T">Target module type intuitively seamlessly.</typeparam>
        /// <returns>Extracted target mapping instance perfectly securely seamlessly.</returns>
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
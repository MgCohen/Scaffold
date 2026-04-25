namespace LiveOps.DTO.GameModule
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class GameData
    {

        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        public readonly List<IGameModuleData> ModulesData = new List<IGameModuleData>();

        public List<IGameModuleData> GetModules()
        {
            return ModulesData;
        }

        public void AddModuleData(IGameModuleData data)
        {
            if (data != null)
            {
                ModulesData.Add(data);
            }
        }

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

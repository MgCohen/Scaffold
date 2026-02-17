namespace GameModuleDTO.GameModule
{
    public class GameData
    {
        public List<IGameModuleData> modulesData = new List<IGameModuleData>();
        
        public List<IGameModuleData> GetModules()
        {
            return modulesData;
        }
        
        public void AddModules(List<IGameModuleData> value)
        {
            foreach (IGameModuleData gameData in value)
            {
                AddModuleData(gameData);
            }
        }
        
        public void AddModuleData(IGameModuleData data)
        {
            if (data != null)
            {
                modulesData.Add(data);
            }
        }

        public T GetModuleData<T>() where T : IGameModuleData
        {
            foreach (IGameModuleData module in modulesData)
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
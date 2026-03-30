using System;
using System.IO;
using Newtonsoft.Json;

namespace AAGen
{
    [Serializable]
    public class JsonReport
    {
        /// <summary>
        /// Extra info about Data
        /// </summary>
        public string Summary;
        
        /// <summary>
        /// raw data
        /// </summary>
        public object Data;

        public JsonReport(string summary, object data)
        {
            Summary = summary;
            Data = data;
        }
        
        /// <summary>
        /// Saves a json report file in the persistent data path 
        /// </summary>
        /// <param name="reporter"></param>
        /// <param name="filename"></param>
        /// <param name="summary"></param>
        /// <param name="data"></param>
        public static void SaveJsonReport(Type reporter, string filename, string summary, object data)
        {
            var reportName = reporter.Name;
            var filePath = Path.Combine(Constants.FilePaths.PersistentDataFolder, $"{filename}.json");
            var jsonReport = new JsonReport($"Reporter = {reportName} | " + summary, data);
            FileUtils.SaveToFile(filePath, JsonConvert.SerializeObject(jsonReport, Formatting.Indented));
        }
        
        public static void SaveJsonReport(Type reporter, string summary, object data)
        {
            SaveJsonReport(reporter, reporter.Name, summary, data);
        }
    }
}

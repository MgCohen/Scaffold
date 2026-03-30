using System.IO;
using UnityEngine;

namespace AAGen
{
    internal static class Constants
    {
        public const string PackageShortName = "AAGen";
        public const string PackageShortDescription = "Automated Addressable Grouping Tool";
        
        /// <summary>
        /// Defines constants that are used by for file menu actions.
        /// </summary>
        public static class Menus
        {
            public const int AAGenMenuPriority = 0;
            public const string Root = "Tools/" + PackageShortName + "/";
            public const string AAGenMenuPath = Root + PackageShortName;
        }

        /// <summary>
        /// Defines constants that are used by for context menu actions.
        /// </summary>
        public static class ContextMenus
        {
            public const string Root = PackageShortName + "/";
            public const string RulesMenu = Root + "Rules/";
            public const string InputRulesMenu = RulesMenu + "Input Rules/";
            public const string OutputRulesMenu = RulesMenu + "Output Rules/";
        }
        
        /// <summary>
        /// Defines constants that are used by for file reading and writing actions.
        /// </summary>
        public static class FilePaths
        {
            public static string PersistentDataFolder => Path.Combine(Application.persistentDataPath, PackageShortName) + "/";
            
            public static string DependencyGraphFilePath => Path.Combine(PersistentDataFolder, "DependencyGraph.txt");
            
            //ProcessingStep report paths
            public static string SubGraphReportPath => Path.Combine(PersistentDataFolder, "SubGraphReport.txt");
            public static string GroupLayoutReportPath => Path.Combine(PersistentDataFolder, "GroupLayoutReport.txt");
            public static string CleanupReportPath => Path.Combine(PersistentDataFolder, "CleanupReport.txt");

           
            public static string SummaryReportPath => Path.Combine(Application.persistentDataPath, "SummaryReport.txt");
        }
        
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents a command queue for processing dependency graph generation.
    /// </summary>
    internal class DependencyGraphCommandQueue : CommandQueue
    {
        #region Constants
        /// <summary>
        /// A unique set of file extensions that should be ignored.
        /// </summary>
        private static readonly HashSet<string> k_IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".hlsl", ".asmdef", ".asmref", ".dll", ".unitypackage", ".txt"
        };

        /// <summary>
        /// A collection of file paths to parent directories that should be ignored.
        /// </summary>
        private static readonly string[] k_IgnoredPaths = new[]
        {
            "Assets/AddressableAssetsData",
            "Assets/StreamingAssets",
            "/Editor/",
            "/Resources/"
        };
        
        /// <summary>
        /// The file path of the parent directory that Unity Editor uses for assets.
        /// </summary>
        private const string k_AssetsFolder = "Assets/";

        /// <summary>
        /// The file path of the parent directory that Unity Editor uses for packages.
        /// </summary>
        private const string k_PackagesFolder = "Packages/";
        
        // ToDo: Must depend on project size; higher = fewer pauses, lower = more frequent memory relief
        private const int k_MemReliefStride = 1000;
        #endregion
        
        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;

        /// <summary>
        /// The number of assets added to the dependency graph.
        /// </summary>
        /// <remarks>Used by the summary report values.</remarks>
        private int m_TotalAssetCount = 0;

        /// <summary>
        /// A unique set of file paths to assets that are used by AAGen.
        /// </summary>
        private HashSet<string> m_AAGenSettingsFiles;

        /// <summary>
        /// A collection of file paths to assets associated with a value that indicates whether they should be ignored.
        /// </summary>
        private Dictionary<string, bool> m_AssetIgnoreCache;

        int m_LoadedAssetCount;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="DependencyGraphCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public DependencyGraphCommandQueue(DataContainer dataContainer)
        {
            m_DataContainer = dataContainer;

            Title = nameof(DependencyGraphCommandQueue);
        }

        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public override void PreExecute()
        {
            // Clear the command queue.
            ClearQueue();
            
            // Create a new instance of the dependency graph and cache it in the data container.
            m_DataContainer.DependencyGraph = new DependencyGraph();
            
            // Get a unique set of file paths to assets that are used by AAGen and cache them.
            m_AAGenSettingsFiles = FindAAGenDependencies();
            
            // Create a collection of file paths to assets associated with a value that indicates whether they should be ignored.
            m_AssetIgnoreCache = new Dictionary<string, bool>();
            
            // Get the assets that are used as inputs for the dependency graph.
            var assetPaths = AssetDatabase.GetAllAssetPaths();

            // Cache the number of assets added to the dependency graph.
            m_TotalAssetCount = assetPaths.Length;
            
            // For every file path, perform the following:
            foreach (var assetPath in assetPaths)
            {
                // Create a localized cache for the file path of the asset (so that it can be properly captured by the lambda).
                var path = assetPath;

                // Add a command to add the asset to the dependency graph.
                AddCommand(() => AddAssetToDependencyGraph(path), path);
            }

            // If AAGen should save the dependency graph to a file, then:
            if (m_DataContainer.Settings.SaveGraphOnDisk)
            {
                // Add a command to save the graph to file.
                AddCommand(SaveGraphOnDisk, "Saving DependencyGraph");
            }
        }
        
        /// <summary>
        /// Performs an action after processing the commands in the queue.
        /// </summary>
        public override void PostExecute()
        {
            SaveOutputReportToFile();

            //UnInitialize
            m_AAGenSettingsFiles = null;
            m_AssetIgnoreCache = null;
        }

        /// <summary>
        /// Add an asset file path as an entry in the dependency graph.
        /// </summary>
        /// <param name="assetPath">The file path to the asset.</param>
        private void AddAssetToDependencyGraph(string assetPath)
        {
            // Get a reference to the dependency graph.
            var dependencyGraph = m_DataContainer.DependencyGraph;

            // If the file path should be ignored, then:
            if (SkipAsset(assetPath))
            {
                // Do nothing else.
                return;
            }

            // Otherwise, the file path should not be ignored.

            string[] dependencies = null;
            try
            {
                // Get the file paths of the assets that this asset is directly dependent on.
                dependencies = AssetDatabase.GetDependencies(assetPath, false);
            }
            catch 
            {
                // Best effort in case of a corrupt file. 
                // This type of exceptions can be avoided by using "ScanUnsupportedFiles=true" flag.
                // The case is handled by nullity check after this catch block.
            }

            // If the assets are invalid or there are no assets, then:
            if (dependencies == null || dependencies.Length == 0)
            {
                // Add a node to the dependency graph, using the asset path as the value for the node.
                dependencyGraph.AddNode(assetPath);

                // Do nothing else.
                return;
            }

            // For every asset that the asset is directly dependent on, perform the following: 
            foreach (var dependency in dependencies)
            {
                // If the file path of the dependency should be ignored, then:
                if (SkipAsset(dependency))
                {
                    // Do nothing else.
                    continue;
                }

                // Add an edge from the asset to the dependency. Nodes will be added if they don't already exist.
                dependencyGraph.AddEdge(assetPath, dependency);
            }
        }

        /// <summary>
        /// Determines whether a file path to an asset should be ignored.
        /// </summary>
        /// <param name="assetPath">The file path to an asset.</param>
        /// <returns>A value indicating whether the file path should be ignored.</returns>
        private bool SkipAsset(string assetPath)
        {
            // Attempt to get a value indicating whether a file should be ignored,
            // which is associated with the file path.
            // If there is a value associated with the file path, then:
            if (m_AssetIgnoreCache.TryGetValue(assetPath, out var cachedValue))
            {
                // Return the value indicating whether the file path should be ignored.
                return cachedValue;
            }

            // Determine whether a file path to an asset should be ignored.
            bool shouldIgnoreAsset = ShouldIgnoreAsset(assetPath);

            // Associate the value with the file path to cache it.
            m_AssetIgnoreCache.Add(assetPath, shouldIgnoreAsset);

            // Return the value indicating whether the file path should be ignored.
            return shouldIgnoreAsset;
        }

        /// <summary>
        /// Determines whether a file path to an asset should be ignored.
        /// </summary>
        /// <param name="assetPath">The file path to validate.</param>
        /// <returns>A value indicating whether or not the file path should be ignored.</returns>
        private bool ShouldIgnoreAsset(string assetPath)
        {
            // If the file path is null, contains nothing but whitespace,
            // or the file path locates a folder in the project, then:
            if (string.IsNullOrWhiteSpace(assetPath) ||
                AssetDatabase.IsValidFolder(assetPath))
            {
                // The file path should be ignored.
                return true;
            }

            // Otherwise, the file path is valid, non-empty, and is not a folder.

            // If the file path is located in the Assets directory,
            // or located in the local Packages directory, then:
            if (!assetPath.StartsWith(k_AssetsFolder, StringComparison.OrdinalIgnoreCase) &&
                !assetPath.StartsWith(k_PackagesFolder, StringComparison.OrdinalIgnoreCase))
            {
                // Ignore addressable settings files.
                return true;
            }

            if (assetPath.Contains('[') && assetPath.Contains(']')) //AddressableAssetEntry cannot contain '[ ]'
                return true;

            // Otherwise, the file is in not in the Assets or Packages directory.

            // Extract the extension from the file path.
            var extension = Path.GetExtension(assetPath);

            // If the file extension is contained in the collection of extensions to ignore, then the file is an asset that should be ignored.
            // If the file is an asset that should be ignored, then:
            if (k_IgnoredExtensions.Contains(extension))
            {
                // The file path should be ignored.
                return true;
            }

            // Otherwise, the file has an extension type that should not be ignored.

            // For each parent directory that should be ignored, perform the following:
            foreach (var ignoredPath in k_IgnoredPaths)
            {
                // If the directory is a subset of the file path, then the asset is in the parent directory to ignore.
                // If the asset is in the parent directory to ignore, then:
                if (assetPath.Contains(ignoredPath, StringComparison.OrdinalIgnoreCase))
                {
                    // The file path should be ignored.
                    return true;
                }
            }

            // The file is in a parent directory that should not be ignored.

            // If the file path is an file path to an asset used by AAGen, then:
            if (m_AAGenSettingsFiles.Contains(assetPath))
            {
                // Ignore AAGen settings files.
                return true;
            }

            // If AAGen should scan for unsupported files in Addressables settings and the asset is not supported, then:
            if (m_DataContainer.Settings.ScanForUnsupportedFiles)
            {
                var assetSupported = LoadAssetAndCheckSupported(assetPath);
                
                // Because we're loading almost all assets to check if they're supported,
                // in-editor memory can go really high in large projects and cause crash. 
                // We need to release this memory every once in a while
                m_LoadedAssetCount++;
                if (m_LoadedAssetCount % k_MemReliefStride == 0)
                    EditorUtil.UnloadUnusedEditorMemory();
                
                if (!assetSupported)
                    return true; // The file path should be ignored
            }
            
            if (m_DataContainer.Settings.ScanForUnsupportedFiles && !LoadAssetAndCheckSupported(assetPath))
            {
                // The file path should be ignored.
                return true;
            }

            // The file path should not be ignored.
            return false;
        }

        /// <summary>
        /// Find a unique set of file paths to assets that are used by AAGen.
        /// </summary>
        /// <returns>A unique set of file paths to assets that are used by AAGen.</returns>
        private HashSet<string> FindAAGenDependencies()
        {
            var aagenSettingsFiles = new HashSet<string>();

            // Find all the file paths associated with AAGen assets.
            // They are not meant to be made Addressable, so add them to a unique set of file paths to AAGen assets.
            aagenSettingsFiles.UnionWith(AssetDatabaseUtil.FindAssetPathsForType<AagenSettings>());
            aagenSettingsFiles.UnionWith(AssetDatabaseUtil.FindAssetPathsForType<InputRule>());
            aagenSettingsFiles.UnionWith(AssetDatabaseUtil.FindAssetPathsForType<OutputRule>());
            
            return aagenSettingsFiles;
        }

        /// <summary>
        /// Determines whether a file path is supported by by AAGen.
        /// </summary>
        /// <param name="node">The node in question.</param>
        /// <remarks>A value indicating whether the file path is suuported by AAGen.</remarks>
        private bool LoadAssetAndCheckSupported(string assetPath)
        {
            // Attempt to:
            try
            {
                // Get the type that is associated with the main asset at the file path.
                Type mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

                // If an asset type was invalid, or the asset has no specific type, then:
                if (mainAssetType == null || mainAssetType == typeof(DefaultAsset))
                {
                    // The asset is not supported.
                    return false;
                }

                // Otherwise, the type of the asset is valid and has a specific type.

                // If the asset is not eligible for the build, then:
                if (!AreAssetFlagsEligibleForBuild(assetPath))
                {
                    // The asset is not supported.
                    return false;
                }
            }
            // If an exception is thrown, then:
            catch
            {
                // The asset is not supported.
                return false;
            }

            // The asset is supported.
            return true;
        }

        /// <summary>
        /// Determines if the asset at the file path is eligible for an Addressables build.
        /// </summary>
        /// <param name="path">The file path for the asset in question.</param>
        /// <returns>A value indicating whether the asset at the path is eligible for an Addressables build.</returns>
        private bool AreAssetFlagsEligibleForBuild(string path)
        {
            // Load the main asset at the file path.
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);

            // If the main asset is invalid, then:
            if (asset == null)
            {
                // The asset is not eligible.
                return false;
            }

            // Otherwise, the main asset is valid.

            HideFlags flags = asset.hideFlags;

            // If the asset instance can be saved to file in the build or the Editor, then it is eligible.
            return (flags & HideFlags.DontSave) == 0 &&
                   (flags & HideFlags.DontSaveInBuild) == 0 &&
                   (flags & HideFlags.DontSaveInEditor) == 0;
        }

        /// <summary>
        /// Save the graph to file.
        /// </summary>
        private void SaveGraphOnDisk()
        {
            // Convert the dependency graph into a memento and then serialize the memento to JSON.
            var data = JsonConvert.SerializeObject(m_DataContainer.DependencyGraph.Serialize(), Formatting.None);

            // Save the JSON content to the file. 
            FileUtils.SaveToFile(Constants.FilePaths.DependencyGraphFilePath, data);
        }
        
        void SaveOutputReportToFile()
        {
            if (!m_DataContainer.Settings.ProcessReport.HasFlag(ProcessStepReport.DependencyGraph))
                return;

            Graph<string> graph = m_DataContainer.DependencyGraph.ConvertNodeType(n => n.AssetPath);
            var summary = $"AssetDatabase.GetAllAssetPaths().Count = {m_TotalAssetCount}, " +
                          $" DependencyGraph.NodeCount = {graph.NodeCount})";
            var data = graph;

            JsonReport.SaveJsonReport(GetType(), summary, data);
        }
        #endregion
    }
}

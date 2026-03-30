using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents a command queue that ensures that settings are created and properly configured.
    /// </summary>
    internal class SettingsFilesCommandQueue : CommandQueue
    {
        #region Constants
        /// <summary>
        /// The directory path of the default <see cref="AddressableAssetSettings"/> asset.
        /// </summary>
        private const string DefaultSettingsPath = "Assets/AddressableAssetsData";

        /// <summary>
        /// The name of the default <see cref="AddressableAssetSettings"/> asset.
        /// </summary>
        private const string DefaultSettingsName = "AddressableAssetSettings";

        /// <summary>
        /// The directory path of the default <see cref="AagenSettings"/> asset.
        /// </summary>
        private const string DefaultAagenSettingsFolder = "Assets/AddressableAssetsData/AAGen/";
        #endregion

        #region Static Methods
        /// <summary>
        /// Ensure that the Addressables settings file exists if it did not already exist.
        /// </summary>
        public static void CreateAddressableSettingsIfRequired()
        {
            // If the default Addressable settings instance is invalid, then there is no default Addressables settings.
            // If there is no default Addressables settings, then:
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                // Create the default Addressales settings.
                CreateDefaultAddressableSettings();
            }
        }

        /// <summary>
        /// Create the a new instance of <see cref="AddressableAssetSettings"/> and set it as the default.
        /// </summary>
        private static void CreateDefaultAddressableSettings()
        {
            // Ensure that directories exist at the directory path.
            EnsureDirectoryExists(DefaultSettingsPath);

            // Create an instance of the AddressableAssetSettings,
            // using the default location for the asset, its name,
            // create groups for player data and local packed content,
            // and save the new instance to an asset.
            AddressableAssetSettings settings = AddressableAssetSettings.Create(DefaultSettingsPath, DefaultSettingsName, true, true);

            // Cache the new instance of the settings as the default.
            AddressableAssetSettingsDefaultObject.Settings = settings;

            // Ensure that the files that were modified were saved.
            AssetDatabase.SaveAssets();

            // Ensure that the files that were saved are updated in the Unity Editor's project UI.
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Creates a new <see cref="AagenSettings"/> asset at the file path.
        /// </summary>
        /// <param name="settingsFilePath">The file path where the settings asset will be created at.</param>
        /// <returns>An instance associated with the newly created asset.</returns>
        public static AagenSettings CreateDefaultToolSettingsAtPath(string settingsFilePath)
        {
            // Extract the name of the directory out of the file path of the AAGen settings.
            string directory = Path.GetDirectoryName(settingsFilePath);

            // By default, there is no valid AAGen settings instance.
            AagenSettings settings = null;

            // Attempt to:
            try
            {
                // Pause the asset importer so that any assets created do not immediately automatically import.
                AssetDatabase.StartAssetEditing();

                // Create a new instance of the AAGen settings, and cache it.
                settings = ScriptableObject.CreateInstance<AagenSettings>();

                // Create a default input rule at the directory path.
                // Initialize the input filters with the default input rule.                
                settings.InputRules = new List<InputRule>
                {
                    CreateDefaultInputRule(directory)
                };

                // Create a default output rule at the directory path.
                // Initialize the input filters with the default output rule.
                settings.OutputRules = new List<OutputRule>
                {
                    CreateDefaultOutputRule(directory)
                };

                // Take the instance and create the asset at the file location.
                AssetDatabase.CreateAsset(settings, settingsFilePath);

                // Ensure that the files that were modified were saved.
                AssetDatabase.SaveAssets();
            }
            // If an error occurs in the above block, then:
            catch (Exception e)
            {
                // Log the exception as an error.
                Debug.LogError(e);
            }
            // Whether an error occurred or not, perform the following:
            finally
            {
                // Resume the asset importer so that all assets created are immediately automatically import.
                AssetDatabase.StopAssetEditing();

                // Ensure that the files that were saved are updated in the Unity Editor's project UI.
                AssetDatabase.Refresh();
            }

            // Return the result. If successful, the instance is valid. If not, the instance is invalid.
            return settings;
        }

        /// <summary>
        /// Creates a new <see cref="InputRule"/> asset at the file path.
        /// </summary>
        /// <param name="directoryPath">The file path where the input rule asset will be created at.</param>
        /// <returns>An instance associated with the newly created asset.</returns>
        private static InputRule CreateDefaultInputRule(string directoryPath)
        {
            // Define the file name with extension of the default input rule.
            // Combine the file name with extension with the directory to get the file path of the input rule.
            var inputRulePath = Path.Combine(directoryPath, $"Default{nameof(InputRule)}.asset");

            // Create a new instance of the PathFilterRule class.
            var inputRule = ScriptableObject.CreateInstance<AssetSelectionInputRule>();

            // The current Addressables assets should be included.
            inputRule.m_IncludeCurrentAddressables = true;

            // Take the instance and create the asset at the file location.
            AssetDatabase.CreateAsset(inputRule, inputRulePath);

            // Ensure that the files that were modified were saved.
            AssetDatabase.SaveAssets();

            return inputRule;
        }

        /// <summary>
        /// Creates a new <see cref="OutputRule"/> asset at the file path.
        /// </summary>
        /// <param name="directoryPath">The file path where the output rule asset will be created at.</param>
        /// <returns>An instance associated with the newly created asset.</returns>
        private static OutputRule CreateDefaultOutputRule(string directoryPath)
        {
            // Define the file name with extension of the default output rule.
            // Combine the file name with extension with the directory to get the file path of the output rule.
            var outputRulePath = Path.Combine(directoryPath, $"{nameof(DefaultOutputRule)}.asset");

            // Create a new instance of the DefaultOutputRule class.
            var outputRule = ScriptableObject.CreateInstance<DefaultOutputRule>();

            // Take the instance and create the asset at the file location.
            AssetDatabase.CreateAsset(outputRule, outputRulePath);

            // Ensure that the files that were modified were saved.
            AssetDatabase.SaveAssets();

            return outputRule;
        }

        #region Utils
        /// <summary>
        /// Ensures that directories exist at the directory path.
        /// </summary>
        /// <param name="path">The path of the directory.</param>
        private static void EnsureDirectoryExists(string path)
        {
            // If the directory path given does not exist, then:
            if (!Directory.Exists(path))
            {
                // Attempt to create the folders in the directory path.
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Determines whether the AAGen settings are present in the project.
        /// </summary>
        /// <returns>A value indicating whether the AAGen settings are present in the project.</returns>
        private static bool ToolSettingsExists()
        {
            // Attempt to find all the GUIDs associated with the type.
            // If the collection of asset GUID
            return AssetDatabaseUtil.FindAssetGuidsForType<AagenSettings>().Length > 0;
        }
        #endregion
        #endregion

        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;
        #endregion

        #region Methods
        /// <summary>
        /// Create a new instance of the <see cref="SettingsFilesCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the data container.</param>
        public SettingsFilesCommandQueue(DataContainer dataContainer)
        {
            // Cache the reference.
            m_DataContainer = dataContainer;

            Title = nameof(SettingsFilesCommandQueue);
        }

        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public override void PreExecute()
        {
            // Clear the command queue.
            ClearQueue();

            // Add a command that ensures that Addressables settings file exists if it did not already exists.
            AddCommand(CreateAddressableSettingsIfRequired, nameof(CreateAddressableSettingsIfRequired));

            // Add a command tha ensures that the AAGen settings exist and AAGen is properly configured.
            AddCommand(FindOrCreateDefaultToolSettings, nameof(FindOrCreateDefaultToolSettings));

            // Add a command that ensures that the settings are valid.
            AddCommand(() => m_DataContainer.Settings.Validate(), "Validate Settings");
        }

        /// <summary>
        /// Ensure that the AAGen settings exist and AAGen is properly configured.
        /// </summary>
        /// <exception cref="Exception">Throws an error if the AAGen settings are in the project but not properly configured.</exception>
        private void FindOrCreateDefaultToolSettings()
        {
            // If the data container's reference to AAGen settings are valid, then the settings are loaded.
            // If the AAGen settings are loaded, then:
            if (m_DataContainer.Settings != null)
            {
                // Do nothing else.
                return;
            }

            // Otherwise the AAGen settings are not loaded.

            // If the AAGen settings are present in the project (but not loaded), then:
            if (ToolSettingsExists())
            {
                // Throw an error that lets the user know that they should assign a reference to the settings in the UI.
                throw new Exception($"Cannot find AAGen settings file");
            }

            // Otherwise, there are no AAGen settings files in the project.

            // Create one with default settings
            CreateDefaultToolSettings();
        }

        /// <summary>
        /// Create a new instance of the AAgen settings and set the data container to reference it.
        /// </summary>
        private void CreateDefaultToolSettings()
        {
            // Ensure that directories exist at the directory path.
            EnsureDirectoryExists(DefaultAagenSettingsFolder);

            // Define the file name with extension of the AAGen settings.
            // Combine the file name with extension with the directory to get the file path of the AAGen settings.
            var settingsFilePath = Path.Combine(DefaultAagenSettingsFolder, $"Default{nameof(AagenSettings)}.asset");

            // Create the AAGen settings at the location the user chose.
            var settings = CreateDefaultToolSettingsAtPath(settingsFilePath);

            // Overwrite the data container's settings reference and the file path of the asset with the instance that was created. 
            m_DataContainer.SettingsFilePath = settingsFilePath;
            m_DataContainer.Settings = settings;
        }
        #endregion
    }
}
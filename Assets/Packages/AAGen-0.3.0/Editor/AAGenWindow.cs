using System;
using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents an <see cref="EditorWindow"/> used to process AAGen tasks.
    /// </summary>
    internal class AAGenWindow : EditorWindow, IHasCustomMenu
    {
        #region Constants
        /// <summary>
        /// The UMGUI style to use for creating a frame when drawing the GUI.
        /// </summary>
        private const string k_BoxStyleName = "Box";

        /// <summary>
        /// The standard margin, in pixels, used to when drawing the GUI.
        /// </summary>
        private const float k_Space = 5f;
        
        const string k_CreateSettingsButtonLabel = "Create Settings File";
        
        /// <summary>
        /// The label on the  Quick button.
        /// </summary>
        private const string k_QuickButtonLabel = "Generate Addressable Groups Automatically";
        
        /// <summary>
        /// The width of the Quick button.
        /// </summary>
        private const int k_QuickButtonWidth = 300;
        
        /// <summary>
        /// The height of the quick button.
        /// </summary>
        private const int k_QuickButtonHeight = 30;
        #endregion

        #region Static Methods
        /// <summary>
        /// Show an instance of the <see cref="AAGenWindow"/> class.
        /// </summary>
        [MenuItem(Constants.Menus.AAGenMenuPath, priority = Constants.Menus.AAGenMenuPriority)] 
        public static void ShowWindow()
        {
            // Get an instance of this Window, if it already is open, or create a new one with the title given.
            var window = GetWindow<AAGenWindow>("AAGen");

            // Set the minimum dimensions of the window.
            window.minSize = new Vector2(400, 200); 
        }

        /// <summary>
        /// Determines whether the AAGen settings are present in the project.
        /// </summary>
        /// <returns></returns>
        private static bool ToolSettingsExists()
        {
            // Get a collection of asset guids that are AagenSettings. 
            string[] allSettings = AssetDatabaseUtil.FindAssetGuidsForType<AagenSettings>();

            // If there is at least one object, then the settings exist.
            return allSettings.Length > 0;
        }
        #endregion

        #region Fields
        /// <summary>
        /// A Editor-persistent value used to store the location of the AAGen settings.
        /// </summary>
        private EditorPersistentValue<string> m_SettingsAssetPath = new (null, "EPK_AAG_SettingsPath");

        /// <summary>
        /// A reference to the asset that stores the AAGen settings.
        /// </summary>
        private AagenSettings m_Settings;

        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private DataContainer m_DataContainer;

        /// <summary>
        /// A value indicating whether the window is still processing commands from the command queues.
        /// </summary>
        private bool m_IsProcessing = false;

        /// <summary>
        /// A value indicating whether the commands have been interrupted through cancellation. 
        /// </summary>
        private bool m_IsCancelled = false;

        /// <summary>
        /// The last known time that a command queue was processed.
        /// </summary>
        private double m_LastTime;
        #endregion

        #region Methods
        /// <summary>
        /// Called by the Unity system when the editor window is first created.
        /// </summary>
        private void OnEnable()
        {
            // Get the file path of the AAGen settings asset.
            var assetPath = m_SettingsAssetPath.Value;

            // If the file path of the settings asset is valid and non-empty, then:
            if (!string.IsNullOrEmpty(assetPath))
            {
                // Attempt to load an instance of the settings using the file path as the location of the asset.
                // Cache the reference for later use.
                m_Settings = AssetDatabase.LoadAssetAtPath<AagenSettings>(assetPath);
            }
        }

        /// <summary>
        /// Called by the Unity system when the editor window is closed.
        /// </summary>
        private void OnDisable()
        {
            // If the instance of the settings is valid, then:
            if (m_Settings != null)
            {
                // There is an instance of the settings worth keeping persistent.

                // Get the file path of the asset that is associated with the instance.
                string assetPath = AssetDatabase.GetAssetPath(m_Settings);

                // Cache the file path as a persistent value.
                m_SettingsAssetPath.Value = assetPath;
            }
            else
            {
                // Otherwise, the instance of the settings was invalid.

                // Assume that the file path of the AAGen settings was invalid as well and clear it from persistence.
                m_SettingsAssetPath.ClearPersistentData();
            }
        }

        /// <summary>
        /// Updates the reference to AAGen settings, if the file path the settings asset changes.
        /// </summary>
        private void LoadSettingsFileInEditor()
        {
            // Attempt to load an instance of the settings using the file path from the data container.
            // Cache the reference for later use.
            m_Settings = AssetDatabase.LoadAssetAtPath<AagenSettings>(m_DataContainer.SettingsFilePath);
        }
        
        /// <summary>
        /// Called by the Unity system when the rendering the GUI.
        /// </summary>
        /// <remarks>Called many times in one frame, one for each value that <see cref="Event.current"/> becomes.</remarks>
        private void OnGUI()
        {
            // Determines whether the AAGen settings are present in the project.
            bool settingFileExists = ToolSettingsExists();

            // Create a minimum width layout for the quick button.
            GUILayoutOption buttonMinWidth = GUILayout.MinWidth(k_QuickButtonWidth);

            // Create a height layout for the quick button.
            GUILayoutOption buttonHeight = GUILayout.Height(k_QuickButtonHeight);
            
            // Begin an area where all IMGUI elements within are drawn with a vertical layout.
            GUILayout.BeginVertical(k_BoxStyleName);

            // Add a margin to the item.
            GUILayout.Space(k_Space);

            // If the AAGen settings file exists, then:
            if (settingFileExists)
            {
                // Handles drawing the AAGen settings field.
                Action drawSettings = () =>
                {
                    // Draw a control for updating the reference to the AAGen settings asset.
                    // If the user changes the reference to the AAGen settings asset, then cache a new reference. 
                    m_Settings = (AagenSettings)EditorGUILayout.ObjectField(m_Settings, typeof(AagenSettings), false, buttonMinWidth);
                };

                // Draw a control for updating the reference to the AAGen settings asset.
                DrawCentered(drawSettings, k_QuickButtonWidth); //ToDo: Is this element width still needed?
            }
            
            // Add a margin to the item. 
            GUILayout.Space(k_Space);

            // Handles drawing the quick build button.
            Action drawQuickButton = () =>
            {
                // If the AAGen settings does not exist, then:
                if (!settingFileExists)
                {
                    // Draw a button for generating the Addressables and AAGen settings.
                    // If the button was pressed, then:
                    if (GUILayout.Button(k_CreateSettingsButtonLabel, buttonMinWidth, buttonHeight))
                    {
                        // Process the commands to create the Addressables and AAGen settings as synchonous/blocking process instead.
                        RunBlockingLoop(InitializeSettingsButtonCommands());
                    }
                }
                else
                {
                    // Otherwise, the AAGen settings file exists.

                    // Draw a button for automatically generating the Addressables.
                    // If the button was pressed, then:
                    if (GUILayout.Button(k_QuickButtonLabel, buttonMinWidth, buttonHeight))
                    {
                        var runInBackground = m_Settings != null && m_Settings.RunInBackground;
                        RunAAGenTool(runInBackground);
                    }
                }
                
            }; 

            // Draw the quick button as a centered item.
            DrawCentered(drawQuickButton, k_QuickButtonWidth); //ToDo: Is this element width still needed?

            // Add a margin to the item.
            GUILayout.Space(k_Space);

            // End the vertical layout and resume the previous layout.
            GUILayout.EndVertical();
        }

        public void RunAAGenTool(bool runInBackground)
        {
            // If the commands are already being processed, do nothing
            if (m_IsProcessing)
                return;
            
            if (runInBackground)
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(RunAsyncLoop(InitializeCommands()));
            }
            else
            {
                RunBlockingLoop(InitializeCommands());
            }
        }
        
        /// <summary>
        /// Create a new instance of the <see cref="DataContainer"/> class.
        /// </summary>
        private void InitializeDataContainer()
        {
            // Create a new instance of the data container,
            // using the settings instance and the file path of the asset that the instance was created from. 
            m_DataContainer = new DataContainer
            {
                Settings = m_Settings,
                SettingsFilePath = AssetDatabase.GetAssetPath(m_Settings),
            };

            // Create a logger and assign it to the data container.
            m_DataContainer.Logger = new Logger(m_DataContainer);
        }

        /// <summary>
        /// Generates a sequence of command queues to create the Addressables and AAGen settings.
        /// </summary>
        /// <returns>The sequence of command queues to create the Addressables and AAGen settings.</returns>
        private List<CommandQueue> InitializeSettingsButtonCommands()
        {
            // Create and configure an the data container.
            InitializeDataContainer();

            // Create a list of command queues.
            var commandQueues = new List<CommandQueue>
            {
                // There will always be a command queue for ensuring that settings files are created and properly configured,
                new SettingsFilesCommandQueue(m_DataContainer),
            };

            // and a command queue for ensuring that the settings instance loaded from file,
            var loadFileInEditor = new CommandQueue();
            loadFileInEditor.AddCommand(LoadSettingsFileInEditor);

            // Ping the AAGen settings in the Editor, which forces the Inspector window to focus on it.
            loadFileInEditor.AddCommand(() => EditorGUIUtility.PingObject(m_Settings));
            commandQueues.Add(loadFileInEditor);

            return commandQueues;
        }

        /// <summary>
        /// Generates a sequence of command queues to automatically generate the Addressables.
        /// </summary>
        /// <returns>The sequence of command queues to automatically generate the Addressables.</returns>
        private List<CommandQueue> InitializeCommands()
        {
            // Create and configure an the data container.
            InitializeDataContainer();

            // Create a list of command queues.
            var commandQueues = new List<CommandQueue>
            {
                new SettingsFilesCommandQueue(m_DataContainer),
                new CommandQueue(LoadSettingsFileInEditor, nameof(LoadSettingsFileInEditor)),
            };

            if (m_Settings.LastProcessingStep >= LastProcessingStep.InputAssets)
                commandQueues.Add(new InputAssetsCommandQueue(m_DataContainer));

            if (m_Settings.LastProcessingStep >= LastProcessingStep.GenerateDependencyGraph)
                commandQueues.Add(new DependencyGraphCommandQueue(m_DataContainer));

            if (m_Settings.LastProcessingStep >= LastProcessingStep.GenerateSubGraphs)
                commandQueues.Add(new SubgraphCommandQueue(m_DataContainer));

            if (m_Settings.LastProcessingStep >= LastProcessingStep.GenerateGroupLayout)
            {
                commandQueues.Add(new GroupLayoutCommandQueue(m_DataContainer));
                commandQueues.Add(new OutputRuleCommandQueue(m_DataContainer));
            }

            if (m_Settings.LastProcessingStep >= LastProcessingStep.GenerateAddressableGroups)
                commandQueues.Add(new AddressableGroupCommandQueue(m_DataContainer));

            if (m_Settings.LastProcessingStep >= LastProcessingStep.Cleanup)
                commandQueues.Add(new AddressableCleanupCommandQueue(m_DataContainer));

            return commandQueues;
        }

        /// <summary>
        /// Processes the commands asynchronously.
        /// </summary>
        /// <returns>A sequence of incremental sub-routines that the entire routine is comprised of.</returns>
        private IEnumerator RunAsyncLoop(List<CommandQueue> commandQueues)
        {
            // The window is processing commands.
            m_IsProcessing = true;

            // The command processing has not been interrupted or cancelled.
            m_IsCancelled = false;

            // The last known time that a command was processed. It defaults to zero, indicating that no command has been processed.
            double lastUpdateTime = 0;

            // The minimum interval, in seconds.
            const double editorUpdateInterval = 0.25;

            // For each command queue, perform the following: 
            for (int i = 0; i < commandQueues.Count; i++)
            {
                // Get the current time with reference to the startup of the Unity Editor session.
                // It is the last known time that a command queue was processed.
                m_LastTime = EditorApplication.timeSinceStartup;

                // Get the current command queue.
                var currentQueue = commandQueues[i];

                // Perform any actions associated with this command queue before processing the commands.
                currentQueue.PreExecute();

                // Use the current command queue's index to calculate where the progress of this command queue begins.
                float progressStart = (float)i / commandQueues.Count;

                // Use the next command queue's index to calculate where the progress of this command queue ends (where the next begins).
                float progressEnd = (float)(i + 1) / commandQueues.Count;

                // The progress between one command to another in the queue is initialized at zero.
                int progress = 0;

                // The total amount of commands to process is the commands that are remaining in the queue.
                int totalCount = currentQueue.RemainingCommandCount;

                // The title of the current command queue is the title of the progress bar.
                var progressBarTitle = currentQueue.Title;

                // The info in the progress bar will indicate that a command is processing.
                var progressBarInfo = "Processing ...";
                
                // Create a progress bar using the title, which returns a unique identifier for the progress bar.
                var progressId = Progress.Start(progressBarTitle);

                // Make the progress bar an item that can be cancelled/interrupted.
                Progress.RegisterCancelCallback(progressId, CancelCallback);
                
                // While there are still commands to process remaining in the queue, perform the following: 
                while (currentQueue.RemainingCommandCount > 0)
                {
                    // Create a local reference to the information about the command that was processed. By default, there is no information.
                    var info = string.Empty;

                    // Create a local value indicating whether an exception was thrown. By default, it hasn't.
                    bool error = false;

                    // Create a local reference to an exception that may be thrown. By default, it hasn't and is invalid.
                    Exception exception = null;
                    
                    // Attempt to:
                    try
                    {
                        // Process the next command in the queue.
                        // Get the relevant information about the command that was processed.
                        info = currentQueue.ExecuteNextCommand();
                    }
                    // If an error occurs in the above block, then:
                    catch (Exception e)
                    {
                        // Indicate that an error has occurred. 
                        error = true;

                        // Cache the reference to the error that was thrown.
                        exception = e;
                    }

                    // If the command queue has been interrupted through cancellation, then:
                    if (m_IsCancelled)
                    {
                        // Log information about the cancellation.
                        m_DataContainer.Logger.LogInfo(this, $"Cancelled!");

                        // Remove the cancellation option to the user since it already has been used.
                        Progress.UnregisterCancelCallback(progressId);

                        // Indicate that the commands are no longer being processed.
                        m_IsProcessing = false;

                        // Force any editing on the assets to stop.
                        StopAssetEditingIfNeeded();

                        // Remove the progress bar associated with the identifier from the system.
                        Progress.Remove(progressId);

                        yield break;
                    }

                    // If an exception was thrown, then:
                    if (error)
                    {
                        // Log the exception as an error.
                        Debug.LogException(exception);

                        // Indicate that the commands are no longer being processed.
                        m_IsProcessing = false;

                        // Force any editing on the assets to stop.
                        StopAssetEditingIfNeeded();

                        // Remove the progress bar associated with the identifier from the system.
                        Progress.Remove(progressId);

                        // End the iterator method early and tell the enumerator there are no more elements to process.
                        yield break;
                    }
                    
                    // Increment the progress on the command queue.
                    progress++;

                    // If the processing the command did not result in any info, then use the previous info.
                    // Otherwise, processing the command result in relevant information, so use that as the progress bar info.
                    progressBarInfo = string.IsNullOrEmpty(info) ? progressBarInfo : info;

                    // Get the current time with reference to the startup of the Unity Editor session.
                    // It is the time after a command was processed.
                    var currentTime = EditorApplication.timeSinceStartup;

                    // Get the difference in time.
                    // If the difference is larger than the minimum update interval, then:
                    if (currentTime - lastUpdateTime > editorUpdateInterval)
                    {
                        // The time that a command was processed becomes the last known time that a command was processed.
                        lastUpdateTime = currentTime;

                        // Calculate the current progress by interpolating between the start and end section of this command queue.
                        var percentage = progressStart + ((float)progress / totalCount) * (progressEnd - progressStart);

                        // Update the progress bar with the uniqueu identifier.
                        Progress.Report(progressId, percentage, progressBarInfo);

                        // Returns null as the next element in the iterator block, but continue the iteration afterward.
                        yield return null;
                    }
                }

                // Perform any actions associated with this command queue after the commands have been processed.
                currentQueue.PostExecute();

                // Log detailed information regarding the time that was taken to process the command queue.
                m_DataContainer.Logger.LogDev(this,
                    $"Time Taken for {currentQueue.Title} = {Math.Round(EditorApplication.timeSinceStartup - m_LastTime)}s");

                // Remove the cancellation option to the user since it already has been used.
                Progress.UnregisterCancelCallback(progressId);

                // Remove the progress bar associated with the identifier from the system.
                Progress.Remove(progressId);
            }

            // Indicate that the commands are no longer being processed.
            m_IsProcessing = false;

            bool CancelCallback()
            {
                // The command processing has been interrupted or cancelled.
                m_IsCancelled = true;

                // The method was successful. Continue the interruption.
                return true;
            }
        }

        /// <summary>
        /// Processes the commands synchronously/blocking.
        /// </summary>
        private void RunBlockingLoop(List<CommandQueue> commandQueues)
        {
            // The window is processing commands.
            m_IsProcessing = true;

            // The command processing has not been interrupted or cancelled.
            m_IsCancelled = false;

            // Attempt to:
            try
            {
                // For each command queue, perform the following: 
                for (int i = 0; i < commandQueues.Count; i++)
                {
                    // Get the current time with reference to the startup of the Unity Editor session.
                    // It is the last known time that a command queue was processed.
                    m_LastTime = EditorApplication.timeSinceStartup;

                    // Get the current command queue.
                    var currentQueue = commandQueues[i];

                    // Perform any actions associated with this command queue before processing the commands.
                    currentQueue.PreExecute();

                    // Use the current command queue's index to calculate where the progress of this command queue begins.
                    float progressStart = (float)i / commandQueues.Count;

                    // Use the next command queue's index to calculate where the progress of this command queue ends (where the next begins).
                    float progressEnd = (float)(i + 1) / commandQueues.Count;

                    // The progress between one command to another in the queue is initialized at zero.
                    int progress = 0;

                    // The total amount of commands to process is the commands that are remaining in the queue.
                    int totalCount = currentQueue.RemainingCommandCount;

                    // The title of the current command queue is the title of the progress bar.
                    var progressBarTitle = currentQueue.Title;

                    // The info in the progress bar will indicate that a command is processing.
                    var progressBarInfo = "Processing ...";

                    // While there are still commands to process remaining in the queue, perform the following: 
                    while (currentQueue.RemainingCommandCount > 0)
                    {
                        // Process the next command in the queue. Get the relevant information about the operations that occurred.
                        var info = currentQueue.ExecuteNextCommand();

                        // Increment the progress on the command queue.
                        progress++;

                        // If the processing the command did not result in any info, then use the previous info.
                        // Otherwise, processing the command result in relevant information, so use that as the progress bar info.
                        progressBarInfo = string.IsNullOrEmpty(info) ? progressBarInfo : info;

                        // Calculate the current progress by interpolating between the start and end section of this command queue.
                        // Display a cancellable progress bar, using the title, info, and current progress.
                        // If the progress bar was interrupted through cancellation, then:
                        if (EditorUtility.DisplayCancelableProgressBar(progressBarTitle, progressBarInfo,
                                progressStart + ((float)progress / totalCount) * (progressEnd - progressStart)))
                        {
                            // The processing has been interupted.
                            m_IsCancelled = true;

                            // No not iterate further through the commands in the queue. 
                            break;
                        }
                    }

                    // If the processing of commands has been cancelled, then:
                    if (m_IsCancelled)
                    {
                        // Log information about the cancellation.
                        m_DataContainer.Logger.LogInfo(this, $"Cancelled!");

                        // No not iterate further through the command queues. 
                        break;
                    }

                    // Perform any actions associated with this command queue after the commands have been processed.
                    currentQueue.PostExecute();

                    // Log detailed information regarding the time that was taken to process the command queue.
                    m_DataContainer.Logger.LogDev(this,
                        $"Time Taken for {currentQueue.Title} = {Math.Round(EditorApplication.timeSinceStartup - m_LastTime)}s");
                }
            }
            // If an error occurs in the above block, then:
            catch (Exception e)
            {
                // Log the exception as an error.
                Debug.LogException(e);
            }
            // Whether an error occurred or not, perform the following:
            finally
            {
                // Clear any progress from the progress bar.
                EditorUtility.ClearProgressBar();

                // Indicate that the commands are no longer being processed.
                m_IsProcessing = false;

                // Force any editing on the assets to stop.
                StopAssetEditingIfNeeded();
            }
        }

        /// <summary>
        /// Draws GUI elements centered horizontally within the available width.
        /// </summary>
        /// <param name="drawAction">Handles the IMGUI drawing logic.</param>
        /// <param name="elementWidth">Width of the element to center.</param>
        private void DrawCentered(Action drawAction, float elementWidth)
        {
            // Determine the margin of a centered element.
            float padding = (position.width - elementWidth) / 2;

            // Force IMGUI to render in a horizontal layout. 
            GUILayout.BeginHorizontal();

            // Add a margin to center the item.
            GUILayout.Space(padding);

            // Execute the drawing logic for the item.
            drawAction.Invoke();

            // Add a margin to center the item and leave space for a second.
            GUILayout.Space(padding);
            
            // End the horizontal layout to resume the former layout.
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Force assets to stop being edited.
        /// </summary>
        private void StopAssetEditingIfNeeded()
        {
            // If the assets are being edited, then:
            if (m_DataContainer.AssetEditingInProgress)
            {
                // Force the assets to stop being edited. 
                AssetDatabase.StopAssetEditing();

                // Let the rest of the AAGen system know that the assets are no longer being edited.
                m_DataContainer.AssetEditingInProgress = false;
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Locate Saved Files"), false, EditorUtil.LocatePersistentDataFolder);
        }

        #endregion
    }
}

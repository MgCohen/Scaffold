using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents processing steps in the AAGen pipeline that can be included or excluded.
    /// </summary>
    public enum LastProcessingStep
    {
        None = 0,
        //Reserved ProcessingStep Order
        InputAssets = 10,
        GenerateDependencyGraph = 11,
        GenerateSubGraphs = 12,
        GenerateGroupLayout = 13,
        GenerateAddressableGroups = 14,
        Cleanup = 15,
        //Reserved ProcessingStep Order
        All = 100
    }

    [Flags]
    public enum ProcessStepReport
    {
        InputAssets = 1 << 0,
        DependencyGraph = 1 << 1,
        SubGraphs = 1 << 2,
        GroupLayout = 1 << 3,
        AddressableGroups = 1 << 4,
        Cleanup = 1 << 5
    }

    /// <summary>
    /// Represents the level of detail for logging.
    /// </summary>
    public enum LogLevelID
    {
        /// <summary>
        /// Indicates that the logger should only log unexpected errors.
        /// </summary>
        OnlyErrors = 0,

        /// <summary>
        /// Indicates that the logger should only log detailed informational messages.
        /// </summary>
        Info       = 1,

        /// <summary>
        /// Indicates that the logger should only log extremely detailed messages.
        /// </summary>
        Developer = 2
    }

    [CreateAssetMenu(menuName = Constants.ContextMenus.Root + "Settings")]
    public class AagenSettings : ScriptableObject
    {
        #region Fields
        /// <summary>
        /// A list of rules that are used to include assets as input to the AAGen pipeline.
        /// </summary>
        [Header("Rules")]
        public List<InputRule> InputRules = new List<InputRule>();

        /// <summary>
        /// A list of rules that are used to exclude or transform group layouts as output to the AAGen pipeline.
        /// </summary>
        public List<OutputRule> OutputRules = new List<OutputRule>();

        /// <summary>
        /// A value indicating that AAGen should remove Addressables entries that are unnecessary.
        /// </summary>
        [Header("Cleanup")]
        [SerializeField]
        [Tooltip("Should the tool remove the addressable asset entries that used to be needed but no longer are")]
        private bool m_RemoveUnnecessaryEntries = false;

        /// <summary>
        /// A value indicating that AAGen should clean up Addressables groups that are empty.
        /// </summary>
        [SerializeField]
        private bool m_RemoveEmptyGroups = true;

        /// <summary>
        /// A value indicating whether scenes that are Addressable should be removed from the build profile.
        /// </summary>
        [SerializeField]
        private bool m_RemoveAddressableScenesFromBuildProfile = true;

        /// <summary>
        /// A value indicating whether Addressables groups in Addressables settings should be sorted.
        /// </summary>
        [SerializeField]
        private bool m_SortAddressableGroups = true;

        /// <summary>
        /// A value indicating whether unsupported files should be detected.
        /// </summary>
        [Header("Process")]
        [SerializeField]
        private bool m_ScanForUnsupportedFiles = false;
        
        /// <summary>
        /// A bitmask used to include or exclude processing steps in the AAGen pipeline.
        /// </summary>
        [SerializeField]
        private LastProcessingStep m_LastProcessingStep = LastProcessingStep.All;

        /// <summary>
        /// A value indictaing that AAGen should process commands in the background.
        /// </summary>
        [SerializeField]
        private bool m_RunInBackground;

        /// <summary>
        /// A value indicating that AAGen should save the dependency graph to a file.
        /// </summary>
        // [SerializeField]
        private bool m_SaveGraphOnDisk = false;

        /// <summary>
        /// A value indicating that AAGen should generate a summary report.
        /// </summary>
        [Header("Reports")]
        [SerializeField]
        ProcessStepReport m_ProcessReport = 0;
        
        /// <summary>
        /// The current level of detail for logging. Defaults to the minimum/critical logging.
        /// </summary>
        [SerializeField]
        private LogLevelID m_LogLevel = LogLevelID.OnlyErrors;
        #endregion

        #region Properties
        /// <summary>
        /// Gets a bitmask used to include or exclude processing steps in the AAGen pipeline.
        /// </summary>
        public LastProcessingStep LastProcessingStep => m_LastProcessingStep;
        
        /// <summary>
        /// Gets the current level of detail for logging.
        /// </summary>
        public LogLevelID LogLevel => m_LogLevel;

        /// <summary>
        /// Gets a value indictaing that AAGen should process commands in the background.
        /// </summary>
        public bool RunInBackground => m_RunInBackground;
        
        /// <summary>
        /// Gets a value indicating that AAGen should save the dependency graph to a file.
        /// </summary>
        public bool SaveGraphOnDisk => m_SaveGraphOnDisk;

        /// <summary>
        /// Gets a value indicating that AAGen should clean up Addressables groups that are empty.
        /// </summary>
        public bool RemoveEmptyGroups => m_RemoveEmptyGroups;

        /// <summary>
        /// Gets a value indicating that AAGen should remove Addressables entries that are unnecessary.
        /// </summary>
        public bool RemoveUnnecessaryEntries => m_RemoveUnnecessaryEntries;

        /// <summary>
        /// Gets a value indicating whether scenes that are Addressable should be removed from the build profile.
        /// </summary>
        public bool RemoveAddressableScenesFromBuildProfile => m_RemoveAddressableScenesFromBuildProfile;

        /// <summary>
        /// Gets a value indicating whether Addressables groups in Addressables settings should be sorted.
        /// </summary>
        public bool SortAddressableGroups => m_SortAddressableGroups;
        
        /// <summary>
        /// Gets a value indicating whether unsupported files should be detected.
        /// </summary>
        public bool ScanForUnsupportedFiles => m_ScanForUnsupportedFiles;

        /// <summary>
        /// A value indicating that AAGen should generate a summary report.
        /// </summary>
        public ProcessStepReport ProcessReport => m_ProcessReport;

        #endregion

        #region Methods
        public void Validate()
        {
        }
        #endregion
    }
}

using System.Collections.Generic;
using Newtonsoft.Json;

namespace AAGen
{
    internal class InputAssetsCommandQueue : CommandQueue
    {
        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;

        HashSet<string> m_IncludedAssets;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="InputAssetsCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public InputAssetsCommandQueue(DataContainer dataContainer) 
        {
            // Cache the reference.
            m_DataContainer = dataContainer;

            Title = nameof(InputAssetsCommandQueue);
        }

        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public override void PreExecute()
        {
            // The assets are the input assets.
            m_DataContainer.InputAssets = new HashSet<string>();
            
            // Clear the command queue.
            ClearQueue();

            // For every input rule in the data container's AAGen settings, perform the following:
            foreach (var inputRule in m_DataContainer.Settings.InputRules)
            {
                // Create a localized cache for the rule (so that it can be properly captured by the lambda).
                var rule = inputRule;
                
                // Add a command that adds the assets from the input rule to the unique set of input assets.
                AddCommand(() => AddInputAssets(rule));
            }
        }
        
        /// <summary>
        /// Performs an action after processing the commands in the queue.
        /// </summary>
        public override void PostExecute()
        {
            SaveOutputReportToFile();
            
            //UnInitialize
            m_IncludedAssets = null;
        }

        /// <summary>
        /// Adds the assets from the input rule to the unique set of input assets.
        /// </summary>
        /// <param name="inputRule">The input rule.</param>
        private void AddInputAssets(InputRule inputRule)
        {
            // Get the file paths of assets that should be included with the input rule. 
            m_IncludedAssets = inputRule.GetIncludedAssets();

            // Add them to the unique set of asset file paths that are included as inputs to the AAGen pipeline.
            m_DataContainer.InputAssets.UnionWith(m_IncludedAssets);
        }

        void SaveOutputReportToFile()
        {
            if (!m_DataContainer.Settings.ProcessReport.HasFlag(ProcessStepReport.InputAssets))
                return;

            var summary = $"(data.Count={m_IncludedAssets.Count})";
            var data = m_IncludedAssets;

            JsonReport.SaveJsonReport(GetType(), summary, data);
        }
        #endregion
    }
}
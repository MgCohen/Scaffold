namespace AAGen
{
    /// <summary>
    /// Represents a command queue that processes output rules.
    /// </summary>
    internal class OutputRuleCommandQueue : CommandQueue
    {
        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="OutputRuleCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public OutputRuleCommandQueue(DataContainer dataContainer)
        {
            m_DataContainer = dataContainer;

            Title = nameof(OutputRuleCommandQueue);
        }

        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public override void PreExecute()
        {
            // Clear the command queue.
            ClearQueue();

            // For every output rule, perform the following:
            foreach (var outputRule in m_DataContainer.Settings.OutputRules)
            {
                // Create a localized cache for the output rule (so that it can be properly captured by the lambda).
                var rule = outputRule;

                // Add a command that initializes the output rule.
                AddCommand(() => rule.Initialize(m_DataContainer));

                // Add a command that selects the output rule.
                AddCommand(() => rule.Select());

                // Add a commmand that refines the output rule.
                AddCommand(() => rule.Refine());

                // Add a command that uninitializes the output rule.
                AddCommand(() => rule.UnInit());
            }
        }

        /// <summary>
        /// Performs an action after processing the commands in the queue.
        /// </summary>
        public override void PostExecute()
        {
            SaveOutputReportToFile();
        }
        
        void SaveOutputReportToFile()
        {
            if (!m_DataContainer.Settings.ProcessReport.HasFlag(ProcessStepReport.GroupLayout)) //Currently uses the same flag as GroupLayout
                return;

            var summary = $"(GroupLayout.Count after applying Output Rules = {m_DataContainer.GroupLayout.Count})";
            var data = m_DataContainer.GroupLayout;

            JsonReport.SaveJsonReport(GetType(), summary, data);
        }
        #endregion
    }
}
using Newtonsoft.Json;

namespace AAGen
{
    /// <summary>
    /// Represents a command queue for processing dependency graph loading from file.
    /// </summary>
    internal class LoadDependencyGraphCommandQueue : CommandQueue
    {
        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="LoadDependencyGraphCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public LoadDependencyGraphCommandQueue(DataContainer dataContainer)
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

            // Add a command to load the dependency graph from file.
            AddCommand(LoadDependencyGraph, "Load DependencyGraph");
        }

        /// <summary>
        /// Loads the dependency graph from file.
        /// </summary>
        private void LoadDependencyGraph()
        {
            // Load the JSON content from the file path of the dependency graph.
            var stringData = FileUtils.LoadFromFile(Constants.FilePaths.DependencyGraphFilePath);

            // Deserialize the JSON content into the dependency graph memento.
            var serializedData = JsonConvert.DeserializeObject<DependencyGraph.SerializedData>(stringData);

            // Extract the actual dependency graph instance from the memento.
            m_DataContainer.DependencyGraph = DependencyGraph.Deserialize(serializedData);
        }
        #endregion
    }
}
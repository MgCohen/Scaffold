namespace Scaffold.GraphFlow.PackageGenerator
{
    internal readonly struct GraphPackageModel
    {
        internal GraphPackageModel(
            string runnerFullyQualified,
            string runnerNamespace,
            string runnerTypeName,
            string graphStem,
            string extension,
            string assetMenu,
            string registryNamespace,
            string graphFrameworkNamespace,
            string? dispatcherBaseMetadataName)
        {
            RunnerFullyQualified = runnerFullyQualified;
            RunnerNamespace = runnerNamespace;
            RunnerTypeName = runnerTypeName;
            GraphStem = graphStem;
            Extension = extension;
            AssetMenu = assetMenu;
            RegistryNamespace = registryNamespace;
            GraphFrameworkNamespace = graphFrameworkNamespace;
            DispatcherBaseMetadataName = dispatcherBaseMetadataName;
        }

        internal string RunnerFullyQualified { get; }
        internal string RunnerNamespace { get; }
        internal string RunnerTypeName { get; }
        internal string GraphStem { get; }
        internal string Extension { get; }
        internal string AssetMenu { get; }
        internal string RegistryNamespace { get; }
        internal string GraphFrameworkNamespace { get; }
        /// <summary>Open generic dispatcher base (e.g. <c>MyDispatcherBase`2</c> metadata name including namespace).</summary>
        internal string? DispatcherBaseMetadataName { get; }
    }
}
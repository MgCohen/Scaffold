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
            string? dispatcherBaseMetadataName,
            int convention)
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
            Convention = convention;
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
        /// <summary>Mirrors <c>PortConvention</c> enum in <c>Scaffold.GraphFlow.AttributesLib</c>: 0=CommandResultPair, 1=AttributedFields, 2=MutableInReadOnlyOut, 3=AllFieldsIn.</summary>
        internal int Convention { get; }
    }
}
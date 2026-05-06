#pragma warning disable SCA0009 // Assembly attributes use global namespace by design
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.Spike;

[assembly: GraphPackage(
    Runner = typeof(SpikeRunner),
    Extension = "spike",
    AssetMenu = "GraphFlow/Spike",
    Convention = PortConvention.AllFieldsIn,
    RegistryNamespace = "Scaffold.GraphFlow.Spike.Generated")]
#pragma warning restore SCA0009

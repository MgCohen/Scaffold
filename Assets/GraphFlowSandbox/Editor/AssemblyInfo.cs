#pragma warning disable SCA0009 // Assembly attributes use global namespace by design
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.M0.Smoke;

[assembly: GraphPackage(
    Runner = typeof(MySmokeRunner),
    Extension = "gfmsmoke",
    AssetMenu = "GraphFlow/M0 Smoke Graph",
    Convention = PortConvention.AllFieldsIn,
    RegistryNamespace = "Scaffold.GraphFlow.M0.Generated",
    DispatcherBase = typeof(MyDispatcherBase<,>))]
#pragma warning restore SCA0009

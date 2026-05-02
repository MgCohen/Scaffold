using Scaffold.EffectGraph;

[assembly: EffectGraphConfig(
    CommandBases = new[] { "Scaffold.EffectGraph.Sample.Editor.GameCommand`1" },
    EntryBases = new[] { "Scaffold.EffectGraph.Sample.Editor.GameEntryPoint" },
    Convention = PortConvention.CommandResultPair,
    RegistryNamespace = "Scaffold.EffectGraph.Sample.Editor.Generated")]

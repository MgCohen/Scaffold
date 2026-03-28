# Scaffold.GraphFlowGenerator

Roslyn source generator that emits `DefinitionTypeId` and `GraphFlowGeneratedRegistration` for types inheriting `GraphNodeDefinitionBase` / `GraphNodeDefinitionBase<T>`.

## Build

```bash
dotnet build Generators/GraphFlowGenerator -c Release
```

The repo ships a checked-in `GraphFlowDefinitions.g.cs` under `Assets/Scripts/Tools/GraphFlow/Runtime/Sample/`. After changing graph definitions, either update that file or run the generator and **merge** output (do not duplicate `DefinitionTypeId` partials).

Optional: add an `<Analyzer Include="...Scaffold.GraphFlowGenerator.dll" />` to `Directory.Build.props` **only after** removing the hand-written `DefinitionTypeId` blocks from `GraphFlowDefinitions.g.cs` so the generator is the single source of truth.

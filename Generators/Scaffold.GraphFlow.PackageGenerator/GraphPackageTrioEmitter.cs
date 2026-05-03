using System.Threading;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class GraphPackageTrioEmitter
    {
        internal static void Emit(SourceProductionContext spc, Compilation compilation, ImmutableArray<GraphPackageModel> packages, CancellationToken cancellationToken)
        {
            if (packages.IsDefaultOrEmpty)
            {
                return;
            }

            var editorAsm = IsEditorAssembly(compilation);
            if (editorAsm && packages.Length > 1)
            {
                ReportMultiPackageBindings(spc, compilation, packages, cancellationToken);
            }

            foreach (var p in packages)
            {
                DispatchOnePackage(spc, compilation, editorAsm, p, cancellationToken);
            }
        }

        // EFG008: a payload that satisfies IGraphAction<R1> AND IGraphAction<R2> for two declared packages is ambiguous.
        // Reported once from the editor pass (avoids duplicate diagnostics across runtime+editor compiles).
        static void ReportMultiPackageBindings(SourceProductionContext spc, Compilation compilation, ImmutableArray<GraphPackageModel> packages, CancellationToken ct)
        {
            var perRunner = new System.Collections.Generic.List<(string runner, INamedTypeSymbol entryIface, INamedTypeSymbol actionIface, IAssemblySymbol asm)>();
            foreach (var p in packages)
            {
                var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, p.RunnerFullyQualified);
                if (runner?.ContainingAssembly == null)
                {
                    continue;
                }

                var markerNs = string.IsNullOrEmpty(p.GraphFrameworkNamespace) ? "Scaffold.GraphFlow" : p.GraphFrameworkNamespace;
                var iEntry = compilation.GetTypeByMetadataName(markerNs + ".IGraphEntry`1");
                var iAction = compilation.GetTypeByMetadataName(markerNs + ".IGraphAction`1");
                if (iEntry == null || iAction == null)
                {
                    continue;
                }

                perRunner.Add((p.RunnerTypeName, iEntry.Construct(runner), iAction.Construct(runner), runner.ContainingAssembly));
            }

            // Collect distinct payload assemblies once so we don't walk the same assembly N times.
            var assemblies = new System.Collections.Generic.HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            foreach (var entry in perRunner)
            {
                assemblies.Add(entry.asm);
            }

            foreach (var asm in assemblies)
            {
                foreach (var type in GraphPayloadTypeWalker.AllNamedTypesInAssembly(asm, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!PayloadDiscovery.IsCandidateType(type))
                    {
                        continue;
                    }

                    string? firstRunner = null;
                    foreach (var (runnerName, entryIface, actionIface, _) in perRunner)
                    {
                        if (!PayloadDiscovery.Implements(type, entryIface) && !PayloadDiscovery.Implements(type, actionIface))
                        {
                            continue;
                        }

                        if (firstRunner == null)
                        {
                            firstRunner = runnerName;
                            continue;
                        }

                        spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFG008_MultiPackageBinding, Diagnostics.LocationOf(type), type.Name, firstRunner, runnerName));
                        break;
                    }
                }
            }
        }

        static void DispatchOnePackage(SourceProductionContext spc, Compilation compilation, bool editorAsm, GraphPackageModel p, CancellationToken cancellationToken)
        {
            // The GraphAsset SO is intentionally NOT emitted — it must be hand-written by the consumer.
            // Unity's MonoScript binding (used by AssetImportContext.AddObjectToAsset) only resolves ScriptableObject types
            // declared in a real .cs file with matching name; generator-emitted virtual files do not satisfy that lookup.
            if (editorAsm)
            {
                AddEditorSources(spc, compilation, p);
                var registrations = new System.Collections.Generic.List<string>();
                GraphPayloadNodeEmitter.Emit(spc, compilation, true, p, cancellationToken, registrations);
                EmitGenericNodeArtifacts(spc, compilation, p, registrations, cancellationToken);
                GraphRegistryEmitter.EmitRegistryFile(spc, compilation, p, registrations);
            }
            else
            {
                GraphPayloadNodeEmitter.Emit(spc, compilation, false, p, cancellationToken);
            }
        }

        static void EmitGenericNodeArtifacts(
            SourceProductionContext spc,
            Compilation compilation,
            GraphPackageModel p,
            System.Collections.Generic.List<string> registrations,
            System.Threading.CancellationToken ct)
        {
            // Generic nodes live in the runner's own assembly (same as payloads). M2 supports a single source
            // assembly per package; consumer-authored nodes in other referenced assemblies are a v2 follow-up.
            var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, p.RunnerFullyQualified);
            var runnerAsm = runner?.ContainingAssembly;
            if (runnerAsm == null)
            {
                return;
            }

            var nodes = GenericNodeParser.Parse(compilation, runnerAsm, ct);
            GraphGenericNodeEmitter.EmitEditorAndRegistrations(spc, compilation, p, nodes, registrations);
        }

        static bool IsEditorAssembly(Compilation compilation)
        {
            var name = compilation.Assembly.Name;
            return name != null && name.EndsWith(".Editor", System.StringComparison.Ordinal);
        }

        static void AddEditorSources(SourceProductionContext spc, Compilation compilation, GraphPackageModel p)
        {
            var graphSb = new StringBuilder();
            AppendEditorGraphFile(graphSb, compilation, p);
            var graphPath = $"{p.GraphStem}Graph.g.cs";
            var graphText = graphSb.ToString();
            var graphSource = SourceText.From(graphText, Encoding.UTF8);
            spc.AddSource(graphPath, graphSource);
            var impSb = new StringBuilder();
            AppendEditorImporterFile(impSb, compilation, p);
            var impPath = $"{p.GraphStem}GraphImporter.g.cs";
            var impText = impSb.ToString();
            var impSource = SourceText.From(impText, Encoding.UTF8);
            spc.AddSource(impPath, impSource);
        }

        static string EditorGraphToolkitNamespace(Compilation compilation)
        {
            return EditorPackageRoot(compilation) + ".Editor.GToolkit";
        }

        static string EditorImporterNamespace(Compilation compilation)
        {
            return EditorPackageRoot(compilation) + ".Editor";
        }

        static string EditorPackageRoot(Compilation compilation)
        {
            var name = compilation.Assembly.Name ?? "";
            const string suffix = ".Editor";
            if (name.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - suffix.Length);
            }

            return name;
        }

        static void AppendEditorGraphFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            AppendEditorGraphHeader(sb, compilation, p);
            sb.AppendLine();
            sb.AppendLine($"namespace {EditorGraphToolkitNamespace(compilation)}");
            sb.AppendLine("{");
            AppendEditorGraphBody(sb, p);
            sb.AppendLine("}");
        }

        static void AppendEditorGraphHeader(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            var pkgRoot = EditorPackageRoot(compilation);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine($"using {pkgRoot};");
            AppendRunnerUsingIfNeeded(sb, p);
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine("using UnityEditor;");
        }

        static void AppendRunnerUsingIfNeeded(StringBuilder sb, GraphPackageModel p)
        {
            if (string.IsNullOrEmpty(p.RunnerNamespace))
            {
                return;
            }

            sb.AppendLine($"using {p.RunnerNamespace};");
        }

        static void AppendEditorGraphBody(StringBuilder sb, GraphPackageModel p)
        {
            sb.AppendLine("    [Serializable]");
            sb.AppendLine("    [Graph(AssetExtension)]");
            sb.AppendLine($"    public sealed class {p.GraphStem}Graph : Graph<{p.RunnerTypeName}>");
            sb.AppendLine("    {");
            AppendEditorGraphConstantsAndMenu(sb, p);
            sb.AppendLine("    }");
        }

        static void AppendEditorGraphConstantsAndMenu(StringBuilder sb, GraphPackageModel p)
        {
            var extLit = EscapeString(p.Extension);
            var promptLit = EscapeString(CreatePromptTitle(p.AssetMenu));
            var menuLit = EscapeString(MenuItemPath(p.AssetMenu));
            sb.AppendLine($"        internal const string AssetExtension = \"{extLit}\";");
            sb.AppendLine($"        const string k_CreateMenuName = \"{promptLit}\";");
            sb.AppendLine($"        [MenuItem(\"{menuLit}\")]");
            sb.AppendLine($"        static void CreateAssetFile() =>");
            sb.AppendLine($"            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<{p.GraphStem}Graph>(k_CreateMenuName);");
        }

        static string MenuItemPath(string assetMenu)
        {
            return "Assets/Create/" + assetMenu;
        }

        static string CreatePromptTitle(string assetMenu)
        {
            return assetMenu.Replace("/", " ");
        }

        static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static void AppendEditorImporterFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine($"using {EditorGraphToolkitNamespace(compilation)};");
            AppendRunnerUsingIfNeeded(sb, p);
            sb.AppendLine($"using {GraphRegistryEmitter.ResolveRegistryNamespace(p, compilation)};");
            sb.AppendLine("using UnityEditor.AssetImporters;");
            sb.AppendLine();
            sb.AppendLine($"namespace {EditorImporterNamespace(compilation)}");
            sb.AppendLine("{");
            AppendImporterBody(sb, p);
            sb.AppendLine("}");
        }

        static void AppendImporterBody(StringBuilder sb, GraphPackageModel p)
        {
            sb.AppendLine($"    [ScriptedImporter(1, {p.GraphStem}Graph.AssetExtension)]");
            sb.AppendLine($"    public sealed class {p.GraphStem}GraphImporter : GraphAssetImporterBase<{p.GraphStem}Graph, {p.RunnerTypeName}, {p.GraphStem}GraphAsset>");
            sb.AppendLine("    {");
            sb.AppendLine($"        protected override GraphPackageRegistry<{p.RunnerTypeName}> Registry => {GraphRegistryEmitter.RegistryTypeName(p)}.Instance;");
            sb.AppendLine("    }");
        }
    }
}

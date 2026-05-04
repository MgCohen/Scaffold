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
        // (D5: entries are no longer runner-typed and can't be per-package-ambiguous; only IGraphAction<R> stays runner-typed.)
        static void ReportMultiPackageBindings(SourceProductionContext spc, Compilation compilation, ImmutableArray<GraphPackageModel> packages, CancellationToken ct)
        {
            var perRunner = new System.Collections.Generic.List<(string runner, INamedTypeSymbol actionIface, IAssemblySymbol asm)>();
            foreach (var p in packages)
            {
                var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, p.RunnerFullyQualified);
                if (runner?.ContainingAssembly == null)
                {
                    continue;
                }

                var markerNs = string.IsNullOrEmpty(p.GraphFrameworkNamespace) ? "Scaffold.GraphFlow" : p.GraphFrameworkNamespace;
                var iAction = compilation.GetTypeByMetadataName(markerNs + ".IGraphAction`1");
                if (iAction == null)
                {
                    continue;
                }

                perRunner.Add((p.RunnerTypeName, iAction.Construct(runner), runner.ContainingAssembly));
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
                    foreach (var (runnerName, actionIface, _) in perRunner)
                    {
                        if (!PayloadDiscovery.Implements(type, actionIface))
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
                EmitGenericNodeArtifacts(spc, compilation, p, registrations, cancellationToken, editorAssembly: true);
                GraphRegistryEmitter.EmitRegistryFile(spc, compilation, p, registrations);
            }
            else
            {
                GraphPayloadNodeEmitter.Emit(spc, compilation, false, p, cancellationToken);
                EmitGenericNodeArtifacts(spc, compilation, p, registrations: null, cancellationToken, editorAssembly: false);
            }
        }

        static void EmitGenericNodeArtifacts(
            SourceProductionContext spc,
            Compilation compilation,
            GraphPackageModel p,
            System.Collections.Generic.List<string>? registrations,
            System.Threading.CancellationToken ct,
            bool editorAssembly)
        {
            // D6: walk every assembly that references the GraphFlow runtime asm, plus the runner's
            // own asm, plus the current compilation's asm. The "references the GraphFlow runtime"
            // filter is the firewall — only asms that already reference the package can possibly
            // *define* [GraphNode]-attributed types, so this excludes Unity engine asms / 3rd-party
            // SDKs without any consumer ceremony. Built-in nodes in the package light up
            // automatically (the package runtime asm references itself trivially).
            var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, p.RunnerFullyQualified);
            var runnerAsm = runner?.ContainingAssembly;
            if (runnerAsm == null)
            {
                return;
            }

            const string GraphFlowRuntimeAsmName = "Scaffold.GraphFlow";
            var seen = new System.Collections.Generic.HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            var asms = new System.Collections.Generic.List<IAssemblySymbol>();

            void Consider(IAssemblySymbol asm)
            {
                if (asm == null || !seen.Add(asm)) return;
                // Always include runnerAsm and the current compilation asm (which IS the runner asm
                // for the package's own consumers), plus the package runtime asm itself. For other
                // asms, only include them if they reference Scaffold.GraphFlow.
                if (SymbolEqualityComparer.Default.Equals(asm, runnerAsm) ||
                    SymbolEqualityComparer.Default.Equals(asm, compilation.Assembly) ||
                    asm.Name == GraphFlowRuntimeAsmName)
                {
                    asms.Add(asm);
                    return;
                }
                foreach (var refName in asm.Modules)
                {
                    foreach (var refAsm in refName.ReferencedAssemblySymbols)
                    {
                        if (refAsm.Name == GraphFlowRuntimeAsmName)
                        {
                            asms.Add(asm);
                            return;
                        }
                    }
                }
            }

            Consider(compilation.Assembly);
            Consider(runnerAsm);
            foreach (var refAsm in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                Consider(refAsm);
            }

            // Aggregate + deduplicate parsed nodes across asms. Keys are (ns, type-name) since the
            // type-name is unique within the package built-ins / consumer asms in practice. We tag
            // each model with whether it was sourced from the *current* compilation so the runtime
            // emit pass only emits the partial-class completion in the asm where the source actually
            // lives — emitting it into a consumer asm declares a *new* (empty) class in that asm.
            var dedupe = new System.Collections.Generic.HashSet<string>();
            var allNodes = ImmutableArray.CreateBuilder<GenericNodeModel>();
            var inCurrentCompilation = new System.Collections.Generic.HashSet<string>();
            foreach (var asm in asms)
            {
                var parsed = GenericNodeParser.Parse(compilation, asm, ct);
                var isCurrent = SymbolEqualityComparer.Default.Equals(asm, compilation.Assembly);
                foreach (var n in parsed)
                {
                    var key = (n.TypeNamespace ?? "") + "." + n.TypeName;
                    if (dedupe.Add(key))
                    {
                        allNodes.Add(n);
                        if (isCurrent) inCurrentCompilation.Add(key);
                    }
                }
            }

            GraphGenericNodeEmitter.EmitForPackage(spc, compilation, p, allNodes.ToImmutable(), inCurrentCompilation, registrations, editorAssembly);
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
            var valSb = new StringBuilder();
            AppendEditorGraphValidationFile(valSb, compilation, p);
            var valPath = $"{p.GraphStem}GraphValidation.g.cs";
            spc.AddSource(valPath, SourceText.From(valSb.ToString(), Encoding.UTF8));
        }

        static void AppendEditorGraphValidationFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            // Per ExecPlan-v2 M2 milestone — initial OnGraphChanged rule set:
            //   EFG-V01 unsupported node type           (error)   — no registry entry; bake will fail.
            //   EFG-V02 duplicate entry                 (warning) — bake keeps the first occurrence.
            //   EFG-V03 edge port pairing mismatch      (error)   — flow↔data wire, or unknown port.
            //   EFG-V04 unterminated run-flow path      (warning) — flow output unconnected; path
            //                                                       does not reach a Return/Cancel/
            //                                                       ReturnBool terminator.
            //   EFG-V05 required-input-unwired          — deferred to M4 (needs [Required] metadata
            //                                             we haven't designed yet).
            //   EFG-V06 type-mismatch on data edges     — deferred; GraphToolkit refuses heterogeneous
            //                                             port wiring at the UI level so this is not
            //                                             reachable through the authoring surface today.
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine($"using {GraphRegistryEmitter.EditorRegistryNamespace};");
            AppendRunnerUsingIfNeeded(sb, p);
            sb.AppendLine($"using {GraphRegistryEmitter.ResolveRegistryNamespace(p, compilation)};");
            sb.AppendLine();
            sb.AppendLine($"namespace {EditorGraphToolkitNamespace(compilation)}");
            sb.AppendLine("{");
            sb.AppendLine($"    partial class {p.GraphStem}Graph");
            sb.AppendLine("    {");
            sb.AppendLine("        public override void OnGraphChanged(GraphLogger logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            base.OnGraphChanged(logger);");
            sb.AppendLine($"            var registry = {GraphRegistryEmitter.RegistryTypeName(p)}.Instance;");
            sb.AppendLine("            var registrations = new Dictionary<INode, GraphPackageRegistry<" + p.RunnerTypeName + ">.NodeRegistration>();");
            sb.AppendLine("            var entriesByType = new Dictionary<string, List<INode>>();");
            sb.AppendLine("            foreach (var node in GetNodes())");
            sb.AppendLine("            {");
            sb.AppendLine("                var reg = registry.Lookup(node.GetType());");
            sb.AppendLine("                if (reg == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    logger.LogError($\"[EFG-V01] Unsupported node type {node.GetType().Name} — no registry entry. Re-baking will fail.\", node);");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
            sb.AppendLine("                registrations[node] = reg;");
            sb.AppendLine("                if (reg.EntryTypeId == null) continue;");
            sb.AppendLine("                if (!entriesByType.TryGetValue(reg.EntryTypeId, out var list))");
            sb.AppendLine("                {");
            sb.AppendLine("                    list = new List<INode>();");
            sb.AppendLine("                    entriesByType[reg.EntryTypeId] = list;");
            sb.AppendLine("                }");
            sb.AppendLine("                list.Add(node);");
            sb.AppendLine("            }");
            sb.AppendLine("            foreach (var kv in entriesByType)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (kv.Value.Count <= 1) continue;");
            sb.AppendLine("                foreach (var dup in kv.Value.Skip(1))");
            sb.AppendLine("                {");
            sb.AppendLine("                    logger.LogWarning($\"[EFG-V02] Duplicate entry node for '{kv.Key}'. The bake step keeps only the first occurrence.\", dup);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            ValidateEdgePairings(registrations, logger);");
            sb.AppendLine("            ValidateRunFlowTerminates(registrations, logger);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        static void ValidateEdgePairings(Dictionary<INode, GraphPackageRegistry<" + p.RunnerTypeName + ">.NodeRegistration> registrations, GraphLogger logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            var connected = new List<IPort>();");
            sb.AppendLine("            foreach (var kv in registrations)");
            sb.AppendLine("            {");
            sb.AppendLine("                var fromNode = kv.Key;");
            sb.AppendLine("                var fromReg = kv.Value;");
            sb.AppendLine("                foreach (var port in fromNode.GetOutputPorts())");
            sb.AppendLine("                {");
            sb.AppendLine("                    var fromIsFlow = fromReg.FlowOutputPortIds.ContainsKey(port.name);");
            sb.AppendLine("                    var fromIsData = fromReg.DataOutputPortIds.ContainsKey(port.name);");
            sb.AppendLine("                    connected.Clear();");
            sb.AppendLine("                    port.GetConnectedPorts(connected);");
            sb.AppendLine("                    foreach (var other in connected)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        var toNode = other.GetNode();");
            sb.AppendLine("                        if (toNode == null || !registrations.TryGetValue(toNode, out var toReg)) continue;");
            sb.AppendLine("                        var toIsFlow = toReg.FlowInputPortIds.ContainsKey(other.name);");
            sb.AppendLine("                        var toIsData = toReg.DataInputPortIds.ContainsKey(other.name);");
            sb.AppendLine("                        if (fromIsFlow && toIsFlow) continue;");
            sb.AppendLine("                        if (fromIsData && toIsData) continue;");
            sb.AppendLine("                        logger.LogError($\"[EFG-V03] Edge {fromNode.GetType().Name}.{port.name} → {toNode.GetType().Name}.{other.name} pairs incompatible port kinds (flow↔data or unknown port).\", fromNode);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        static void ValidateRunFlowTerminates(Dictionary<INode, GraphPackageRegistry<" + p.RunnerTypeName + ">.NodeRegistration> registrations, GraphLogger logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Walk forward from every entry node along its flow outputs. A node is a flow");
            sb.AppendLine("            // terminator iff its registry entry has zero FlowOutputPortIds (Return/Cancel/");
            sb.AppendLine("            // ReturnBool, plus any future custom terminator). Warn when a flow output is");
            sb.AppendLine("            // unwired and the owning node is itself not a terminator — that path falls off");
            sb.AppendLine("            // the end of the graph instead of reaching an explicit Return/Cancel.");
            sb.AppendLine("            var connected = new List<IPort>();");
            sb.AppendLine("            var visited = new HashSet<INode>();");
            sb.AppendLine("            var queue = new Queue<INode>();");
            sb.AppendLine("            foreach (var kv in registrations)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (kv.Value.EntryTypeId == null) continue;");
            sb.AppendLine("                queue.Enqueue(kv.Key);");
            sb.AppendLine("                visited.Clear();");
            sb.AppendLine("                while (queue.Count > 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var node = queue.Dequeue();");
            sb.AppendLine("                    if (!visited.Add(node)) continue;");
            sb.AppendLine("                    if (!registrations.TryGetValue(node, out var reg)) continue;");
            sb.AppendLine("                    if (reg.FlowOutputPortIds.Count == 0) continue; // terminator");
            sb.AppendLine("                    foreach (var port in node.GetOutputPorts())");
            sb.AppendLine("                    {");
            sb.AppendLine("                        if (!reg.FlowOutputPortIds.ContainsKey(port.name)) continue;");
            sb.AppendLine("                        connected.Clear();");
            sb.AppendLine("                        port.GetConnectedPorts(connected);");
            sb.AppendLine("                        if (connected.Count == 0)");
            sb.AppendLine("                        {");
            sb.AppendLine("                            logger.LogWarning($\"[EFG-V04] Flow path {node.GetType().Name}.{port.name} ends without a Return/Cancel terminator — the executor will simply stop.\", node);");
            sb.AppendLine("                            continue;");
            sb.AppendLine("                        }");
            sb.AppendLine("                        foreach (var other in connected)");
            sb.AppendLine("                        {");
            sb.AppendLine("                            var nextNode = other.GetNode();");
            sb.AppendLine("                            if (nextNode != null) queue.Enqueue(nextNode);");
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
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
            sb.AppendLine($"using {GraphRegistryEmitter.PackageEditorGToolkitNamespace};");
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
            // partial sealed: validation rules land in <Stem>GraphValidation.g.cs (separate partial),
            // and consumers can hand-write further partial extensions if they need to.
            sb.AppendLine("    [Serializable]");
            sb.AppendLine("    [Graph(AssetExtension)]");
            sb.AppendLine($"    public sealed partial class {p.GraphStem}Graph : Graph<{p.RunnerTypeName}>");
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
            sb.AppendLine($"using {GraphRegistryEmitter.EditorRegistryNamespace};");
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

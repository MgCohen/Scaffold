using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class GraphPayloadNodeEmitter
    {
        internal static void Emit(SourceProductionContext spc, Compilation compilation, bool editorAssembly, GraphPackageModel package, CancellationToken ct, System.Collections.Generic.List<string>? editorRegistrations = null)
        {
            var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, package.RunnerFullyQualified);
            if (runner == null)
            {
                return;
            }

            var markerNs = string.IsNullOrEmpty(package.GraphFrameworkNamespace) ? "Scaffold.GraphFlow" : package.GraphFrameworkNamespace;
            var iEntry = compilation.GetTypeByMetadataName(markerNs + ".IGraphEntry");
            var iAction = compilation.GetTypeByMetadataName(markerNs + ".IGraphAction`1");
            var iExec = compilation.GetTypeByMetadataName(markerNs + ".IExecutable`1");
            var graphEntryAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphEntryAttribute");
            var graphCmdAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphCommandPairAttribute");
            var graphPortAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphPortAttribute");
            var inAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.InAttribute");
            var outAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.OutAttribute");
            if (iEntry == null || iAction == null || graphEntryAttr == null || graphPortAttr == null)
            {
                return;
            }

            var actionIface = iAction.Construct(runner);
            var execIface = iExec?.Construct(runner);
            var payloadAssembly = runner.ContainingAssembly;
            if (payloadAssembly is null)
            {
                return;
            }

            foreach (var type in GraphPayloadTypeWalker.AllNamedTypesInAssembly(payloadAssembly, ct))
            {
                ct.ThrowIfCancellationRequested();

                if (!PayloadDiscovery.IsCandidateType(type))
                {
                    continue;
                }

                if (PayloadDiscovery.IsGraphEntry(type, iEntry))
                {
                    if (!PayloadDiscovery.HasGraphEntryAttribute(type, graphEntryAttr))
                    {
                        continue;
                    }

                    if (editorAssembly)
                    {
                        EmitEntryEditor(spc, type, compilation, graphPortAttr);
                        if (editorRegistrations != null)
                        {
                            editorRegistrations.Add(BuildEntryRegistration(type, package, compilation, graphPortAttr));
                        }
                    }
                    else
                    {
                        EmitEntryRuntime(spc, type, package.RunnerTypeName, graphPortAttr);
                    }

                    continue;
                }

                // Mode 2 command/result pairs.
                if (graphCmdAttr != null &&
                    PayloadDiscovery.TryReadCommandPairAttribute(type, graphCmdAttr, out var resultType))
                {
                    if (package.DispatcherBaseMetadataName == null)
                    {
                        continue;
                    }

                    var openDisp = compilation.GetTypeByMetadataName(package.DispatcherBaseMetadataName);
                    if (openDisp is not INamedTypeSymbol { TypeParameters.Length: 2 } openNamed)
                    {
                        continue;
                    }

                    if (resultType == null)
                    {
                        resultType = PayloadDiscovery.FindResultTypeFromDispatcherSubclass(payloadAssembly, openNamed, type, ct);
                        if (resultType == null)
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFG005_CommandPairMissingResult, Diagnostics.LocationOf(type), type.Name));
                            continue;
                        }
                    }

                    var closedDisp = openNamed.Construct(type, resultType);
                    if (editorAssembly)
                    {
                        EmitCommandEditor(spc, type, resultType, compilation, graphPortAttr);
                        if (editorRegistrations != null)
                        {
                            editorRegistrations.Add(BuildCommandRegistration(type, resultType, package, compilation, graphPortAttr));
                        }
                    }
                    else
                    {
                        EmitCommandRuntime(
                            spc,
                            type,
                            resultType,
                            closedDisp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            package.RunnerTypeName,
                            graphPortAttr);
                    }

                    continue;
                }

                if (!PayloadDiscovery.Implements(type, actionIface))
                {
                    continue;
                }

                if (execIface != null && PayloadDiscovery.Implements(type, execIface))
                {
                    var (inputs, _) = FieldClassifier.Classify(type, package.Convention, inAttr, outAttr);
                    if (editorAssembly)
                    {
                        EmitExecutableEditor(spc, type, inputs, compilation);
                        if (editorRegistrations != null)
                        {
                            editorRegistrations.Add(BuildExecutableActionRegistration(type, inputs, package, compilation, graphPortAttr));
                        }
                    }
                    else
                    {
                        EmitExecutableRuntime(spc, type, inputs, package.RunnerTypeName, graphPortAttr);
                    }

                    continue;
                }

                if (editorAssembly)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFG007_NoExecutionPath, Diagnostics.LocationOf(type), type.Name, package.RunnerTypeName));
                }
            }
        }

        static System.Collections.Generic.IReadOnlyList<(string Name, string CSharpType)> FieldsWithPort(INamedTypeSymbol payload, INamedTypeSymbol graphPortAttr)
        {
            var result = new System.Collections.Generic.List<(string, string)>();
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (PayloadDiscovery.HasGraphPort(f, graphPortAttr))
                {
                    result.Add((f.Name, TypeFmt.Simple(f.Type)));
                }
            }

            return result;
        }

        static string BuildEntryRegistration(INamedTypeSymbol payload, GraphPackageModel package, Compilation compilation, INamedTypeSymbol graphPortAttr)
        {
            var leaf = payload.Name;
            var editorTypeFq = GraphCompilationNames.EditorGraphToolkitNamespace(compilation) + "." + leaf + "EditorNode";
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            var runtimeTypeFq = string.IsNullOrEmpty(typeNs) ? leaf + "Runtime" : typeNs + "." + leaf + "Runtime";
            var payloadTypeFq = string.IsNullOrEmpty(typeNs) ? leaf : typeNs + "." + leaf;
            var runnerFq = GraphRegistryEmitter.TrimGlobal(package.RunnerFullyQualified);
            var fields = FieldsWithPort(payload, graphPortAttr);
            var dataOuts = new System.Collections.Generic.List<string>();
            foreach (var f in fields)
            {
                dataOuts.Add(f.Name);
            }

            return GraphRegistryEmitter.BuildEntryRegistrationBlock(runnerFq, editorTypeFq, runtimeTypeFq, payloadTypeFq, "FlowOut", dataOuts, null);
        }

        static string BuildExecutableActionRegistration(INamedTypeSymbol payload, System.Collections.Generic.IReadOnlyList<IFieldSymbol> inputs, GraphPackageModel package, Compilation compilation, INamedTypeSymbol graphPortAttr)
        {
            var leaf = payload.Name;
            var editorTypeFq = GraphCompilationNames.EditorGraphToolkitNamespace(compilation) + "." + leaf + "DispatcherEditorNode";
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            var runtimeTypeFq = string.IsNullOrEmpty(typeNs) ? leaf + "DispatcherRuntime" : typeNs + "." + leaf + "DispatcherRuntime";
            var runnerFq = GraphRegistryEmitter.TrimGlobal(package.RunnerFullyQualified);
            var dataIns = FieldsWithPortFiltered(inputs, graphPortAttr);
            return GraphRegistryEmitter.BuildExecutableActionRegistrationBlock(runnerFq, editorTypeFq, runtimeTypeFq, "FlowIn", dataIns);
        }

        static System.Collections.Generic.IReadOnlyList<(string Name, string CSharpType)> FieldsWithPortFiltered(System.Collections.Generic.IReadOnlyList<IFieldSymbol> fields, INamedTypeSymbol graphPortAttr)
        {
            var result = new System.Collections.Generic.List<(string, string)>();
            foreach (var f in fields)
            {
                if (PayloadDiscovery.HasGraphPort(f, graphPortAttr))
                {
                    result.Add((f.Name, TypeFmt.Simple(f.Type)));
                }
            }

            return result;
        }

        static string BuildCommandRegistration(INamedTypeSymbol cmd, INamedTypeSymbol result, GraphPackageModel package, Compilation compilation, INamedTypeSymbol graphPortAttr)
        {
            var leaf = cmd.Name;
            var editorTypeFq = GraphCompilationNames.EditorGraphToolkitNamespace(compilation) + "." + leaf + "DispatcherEditorNode";
            var typeNs = cmd.ContainingNamespace.IsGlobalNamespace ? "" : cmd.ContainingNamespace.ToDisplayString();
            var runtimeTypeFq = string.IsNullOrEmpty(typeNs) ? leaf + "DispatcherRuntime" : typeNs + "." + leaf + "DispatcherRuntime";
            var runnerFq = GraphRegistryEmitter.TrimGlobal(package.RunnerFullyQualified);
            var dataIns = FieldsWithPort(cmd, graphPortAttr);
            var resultFields = FieldsWithPort(result, graphPortAttr);
            var dataOuts = new System.Collections.Generic.List<string>();
            foreach (var f in resultFields)
            {
                dataOuts.Add(f.Name);
            }

            return GraphRegistryEmitter.BuildCommandRegistrationBlock(runnerFq, editorTypeFq, runtimeTypeFq, "FlowIn", "FlowOut", dataIns, dataOuts);
        }

        static void EmitEntryEditor(SourceProductionContext spc, INamedTypeSymbol payload, Compilation compilation, INamedTypeSymbol graphPortAttr)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var ns = GraphCompilationNames.EditorGraphToolkitNamespace(compilation);
            var portFields = new System.Collections.Generic.List<IFieldSymbol>();
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (PayloadDiscovery.HasGraphPort(f, graphPortAttr))
                {
                    portFields.Add(f);
                }
            }
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {leaf}EditorNode : Node");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const string FlowOutPortName = \"FlowOut\";");

            foreach (var f in portFields)
            {
                sb.AppendLine($"        public const string {f.Name}PortName = \"{f.Name}\";");
            }

            sb.AppendLine();
            sb.AppendLine("        protected override void OnDefinePorts(IPortDefinitionContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            context.AddOutputPort(FlowOutPortName)");
            sb.AppendLine("                .WithDisplayName(string.Empty)");
            sb.AppendLine("                .WithConnectorUI(PortConnectorUI.Arrowhead)");
            sb.AppendLine("                .Build();");

            foreach (var f in portFields)
            {
                var gt = EditorGtPortMethod(f.Type);
                sb.AppendLine($"            context.AddOutputPort{gt}({f.Name}PortName).Build();");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}EditorNode.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static string EditorGtPortMethod(ITypeSymbol t) => TypeFmt.IsInt(t) ? "<int>" : TypeFmt.IsString(t) ? "<string>" : "";

        static void EmitEntryRuntime(SourceProductionContext spc, INamedTypeSymbol payload, string runnerName, INamedTypeSymbol graphPortAttr)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            var fields = FieldsWithPort(payload, graphPortAttr);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Scaffold.GraphFlow;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed partial class {leaf}Runtime : EntryRuntimeNode<{leaf}>");
            sb.AppendLine("    {");
            foreach (var f in fields)
            {
                sb.AppendLine($"        public OutputPort<{f.CSharpType}> {f.Name} = null!;");
                sb.AppendLine();
                sb.AppendLine($"        {f.CSharpType} _{LowerFirst(f.Name)}Value = {TypeFmt.DefaultLiteral(GetFieldType(payload, f.Name))};");
                sb.AppendLine();
            }

            sb.AppendLine($"        public {leaf}Runtime()");
            sb.AppendLine("        {");
            foreach (var f in fields)
            {
                sb.AppendLine($"            {f.Name} = new OutputPort<{f.CSharpType}>(() => _{LowerFirst(f.Name)}Value);");
            }

            foreach (var f in fields)
            {
                sb.AppendLine($"            Ports.Add(\"{f.Name}\", {f.Name});");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override Task Execute(Flow flow)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Payload != null)");
            sb.AppendLine("            {");
            foreach (var f in fields)
            {
                sb.AppendLine($"                _{LowerFirst(f.Name)}Value = Payload.{f.Name};");
            }

            sb.AppendLine("            }");
            sb.AppendLine($"            return flow.GoTo(\"FlowOut\");");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}Runtime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        static ITypeSymbol GetFieldType(INamedTypeSymbol type, string fieldName)
        {
            foreach (var f in PayloadDiscovery.InstanceFields(type))
            {
                if (f.Name == fieldName)
                {
                    return f.Type;
                }
            }

            throw new System.InvalidOperationException($"Field {fieldName} not found on {type.Name}.");
        }

        static void EmitExecutableEditor(SourceProductionContext spc, INamedTypeSymbol payload, System.Collections.Generic.IReadOnlyList<IFieldSymbol> inputs, Compilation compilation)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var ns = GraphCompilationNames.EditorGraphToolkitNamespace(compilation);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {leaf}DispatcherEditorNode : Node");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const string FlowInPortName = \"FlowIn\";");
            foreach (var f in inputs)
            {
                sb.AppendLine($"        public const string {f.Name}PortName = \"{f.Name}\";");
            }

            sb.AppendLine();
            sb.AppendLine("        protected override void OnDefinePorts(IPortDefinitionContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            context.AddInputPort(FlowInPortName)");
            sb.AppendLine("                .WithDisplayName(string.Empty)");
            sb.AppendLine("                .WithConnectorUI(PortConnectorUI.Arrowhead)");
            sb.AppendLine("                .Build();");
            foreach (var f in inputs)
            {
                var gm = EditorGtInputPort(f.Type);
                sb.AppendLine($"            context.AddInputPort{gm}({f.Name}PortName).Build();");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}DispatcherEditorNode.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static string EditorGtInputPort(ITypeSymbol t) => TypeFmt.IsInt(t) ? "<int>" : TypeFmt.IsString(t) ? "<string>" : "";

        static void EmitExecutableRuntime(SourceProductionContext spc, INamedTypeSymbol payload, System.Collections.Generic.IReadOnlyList<IFieldSymbol> inputs, string runnerName, INamedTypeSymbol graphPortAttr)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            var inputFields = FieldsWithPortFiltered(inputs, graphPortAttr);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Scaffold.GraphFlow;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed partial class {leaf}DispatcherRuntime : RuntimeNode<{runnerName}>");
            sb.AppendLine("    {");
            foreach (var f in inputFields)
            {
                sb.AppendLine($"        public {f.CSharpType} {f.Name} = {TypeFmt.DefaultLiteral(GetFieldType(payload, f.Name))};");
                sb.AppendLine();
                sb.AppendLine($"        public InputPort<{f.CSharpType}> In{f.Name} = null!;");
                sb.AppendLine();
            }

            sb.AppendLine($"        public {leaf}DispatcherRuntime()");
            sb.AppendLine("        {");
            foreach (var f in inputFields)
            {
                sb.AppendLine($"            In{f.Name} = new InputPort<{f.CSharpType}>(() => {f.Name});");
            }

            foreach (var f in inputFields)
            {
                sb.AppendLine($"            Ports.Add(\"{f.Name}\", In{f.Name});");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override async Task Execute({runnerName} runner, Flow flow)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var payload = new {leaf}");
            sb.AppendLine("            {");
            foreach (var f in inputFields)
            {
                sb.AppendLine($"                {f.Name} = In{f.Name}.Read(),");
            }

            sb.AppendLine("            };");
            sb.AppendLine("            await payload.Execute(runner).ConfigureAwait(false);");
            sb.AppendLine("            // No flow.GoTo → default-on-no-call is Stop (terminal action).");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}DispatcherRuntime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static void EmitCommandEditor(
            SourceProductionContext spc,
            INamedTypeSymbol cmd,
            INamedTypeSymbol result,
            Compilation compilation,
            INamedTypeSymbol graphPortAttr)
        {
            var sb = new StringBuilder();
            var leaf = cmd.Name;
            var ns = GraphCompilationNames.EditorGraphToolkitNamespace(compilation);
            var cmdPortFields = new System.Collections.Generic.List<IFieldSymbol>();
            foreach (var f in PayloadDiscovery.InstanceFields(cmd))
            {
                if (PayloadDiscovery.HasGraphPort(f, graphPortAttr))
                {
                    cmdPortFields.Add(f);
                }
            }
            var resultPortFields = new System.Collections.Generic.List<IFieldSymbol>();
            foreach (var f in PayloadDiscovery.InstanceFields(result))
            {
                if (PayloadDiscovery.HasGraphPort(f, graphPortAttr))
                {
                    resultPortFields.Add(f);
                }
            }
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {leaf}DispatcherEditorNode : Node");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const string FlowInPortName = \"FlowIn\";");
            sb.AppendLine($"        public const string FlowOutPortName = \"FlowOut\";");
            foreach (var f in cmdPortFields)
            {
                sb.AppendLine($"        public const string {f.Name}PortName = \"{f.Name}\";");
            }

            foreach (var f in resultPortFields)
            {
                sb.AppendLine($"        public const string {f.Name}PortName = \"{f.Name}\";");
            }

            sb.AppendLine();
            sb.AppendLine("        protected override void OnDefinePorts(IPortDefinitionContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            context.AddInputPort(FlowInPortName)");
            sb.AppendLine("                .WithDisplayName(string.Empty)");
            sb.AppendLine("                .WithConnectorUI(PortConnectorUI.Arrowhead)");
            sb.AppendLine("                .Build();");
            sb.AppendLine("            context.AddOutputPort(FlowOutPortName)");
            sb.AppendLine("                .WithDisplayName(string.Empty)");
            sb.AppendLine("                .WithConnectorUI(PortConnectorUI.Arrowhead)");
            sb.AppendLine("                .Build();");
            foreach (var f in cmdPortFields)
            {
                var gm = EditorGtInputPort(f.Type);
                sb.AppendLine($"            context.AddInputPort{gm}({f.Name}PortName).Build();");
            }

            foreach (var f in resultPortFields)
            {
                var om = EditorGtPortMethod(f.Type);
                sb.AppendLine($"            context.AddOutputPort{om}({f.Name}PortName).Build();");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}DispatcherEditorNode.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static void EmitCommandRuntime(
            SourceProductionContext spc,
            INamedTypeSymbol cmd,
            INamedTypeSymbol result,
            string closedDispatcherFq,
            string runnerName,
            INamedTypeSymbol graphPortAttr)
        {
            var sb = new StringBuilder();
            var leaf = cmd.Name;
            var typeNs = cmd.ContainingNamespace.IsGlobalNamespace ? "" : cmd.ContainingNamespace.ToDisplayString();
            var cmdFields = FieldsWithPort(cmd, graphPortAttr);
            var resultFields = FieldsWithPort(result, graphPortAttr);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Scaffold.GraphFlow;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed partial class {leaf}DispatcherRuntime : {closedDispatcherFq}");
            sb.AppendLine("    {");
            foreach (var f in cmdFields)
            {
                sb.AppendLine($"        public {f.CSharpType} {f.Name};");
                sb.AppendLine();
                sb.AppendLine($"        public InputPort<{f.CSharpType}> In{f.Name} = null!;");
                sb.AppendLine();
            }

            foreach (var f in resultFields)
            {
                sb.AppendLine($"        public OutputPort<{f.CSharpType}> {f.Name} = null!;");
                sb.AppendLine();
                sb.AppendLine($"        {f.CSharpType} _{LowerFirst(f.Name)}Value = {TypeFmt.DefaultLiteral(GetFieldType(result, f.Name))};");
                sb.AppendLine();
            }

            sb.AppendLine($"        public {leaf}DispatcherRuntime()");
            sb.AppendLine("        {");
            foreach (var f in cmdFields)
            {
                sb.AppendLine($"            In{f.Name} = new InputPort<{f.CSharpType}>(() => {f.Name});");
            }

            foreach (var f in resultFields)
            {
                sb.AppendLine($"            {f.Name} = new OutputPort<{f.CSharpType}>(() => _{LowerFirst(f.Name)}Value);");
            }

            foreach (var f in cmdFields)
            {
                sb.AppendLine($"            Ports.Add(\"{f.Name}\", In{f.Name});");
            }

            foreach (var f in resultFields)
            {
                sb.AppendLine($"            Ports.Add(\"{f.Name}\", {f.Name});");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        protected override string FlowOutPortName => \"FlowOut\";");
            sb.AppendLine();
            sb.AppendLine($"        protected override {cmd.Name} BuildPayload() => new {cmd.Name} {{ {BuildPayloadInputs(cmdFields)} }};");
            sb.AppendLine();
            AppendWriteOutputsBody(sb, result, resultFields);
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}DispatcherRuntime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static void AppendWriteOutputsBody(StringBuilder sb, INamedTypeSymbol result, System.Collections.Generic.IReadOnlyList<(string Name, string CSharpType)> fields)
        {
            if (fields.Count == 1)
            {
                var f = fields[0];
                sb.AppendLine($"        protected override void WriteOutputs({result.Name} result) => _{LowerFirst(f.Name)}Value = result.{f.Name};");
                return;
            }

            sb.AppendLine($"        protected override void WriteOutputs({result.Name} result)");
            sb.AppendLine("        {");
            foreach (var f in fields)
            {
                sb.AppendLine($"            _{LowerFirst(f.Name)}Value = result.{f.Name};");
            }

            sb.AppendLine("        }");
        }

        static string BuildPayloadInputs(System.Collections.Generic.IReadOnlyList<(string Name, string CSharpType)> cmdFields)
        {
            var sb = new StringBuilder();
            foreach (var f in cmdFields)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"{f.Name} = In{f.Name}.Read()");
            }

            return sb.ToString();
        }
    }

    internal static class PayloadDiscovery
    {
        internal static bool IsCandidateType(INamedTypeSymbol type) =>
            (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) && type.DeclaredAccessibility == Accessibility.Public;

        internal static bool IsGraphEntry(INamedTypeSymbol type, INamedTypeSymbol iEntry)
        {
            foreach (var i in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(i, iEntry))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool Implements(INamedTypeSymbol type, INamedTypeSymbol ifaceConstructed)
        {
            foreach (var i in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(i, ifaceConstructed))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasGraphEntryAttribute(INamedTypeSymbol type, INamedTypeSymbol graphEntryAttr)
        {
            foreach (var a in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphEntryAttr))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryReadCommandPairAttribute(
            INamedTypeSymbol type,
            INamedTypeSymbol graphCmdAttr,
            out INamedTypeSymbol? resultType)
        {
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphCmdAttr))
                {
                    continue;
                }

                resultType = null;
                foreach (var na in a.NamedArguments)
                {
                    if (na.Key == "ResultType" && na.Value.Value is INamedTypeSymbol r)
                    {
                        resultType = r;
                    }
                }

                return true;
            }

            resultType = null;
            return false;
        }

        internal static INamedTypeSymbol? FindResultTypeFromDispatcherSubclass(
            IAssemblySymbol payloadAssembly,
            INamedTypeSymbol openDispatcher,
            INamedTypeSymbol commandType,
            System.Threading.CancellationToken ct)
        {
            foreach (var t in GraphPayloadTypeWalker.AllNamedTypesInAssembly(payloadAssembly, ct))
            {
                for (var b = t.BaseType; b != null; b = b.BaseType)
                {
                    if (!SymbolEqualityComparer.Default.Equals(b.OriginalDefinition, openDispatcher))
                    {
                        continue;
                    }

                    if (b.TypeArguments.Length != 2)
                    {
                        continue;
                    }

                    if (!SymbolEqualityComparer.Default.Equals(b.TypeArguments[0], commandType))
                    {
                        continue;
                    }

                    if (b.TypeArguments[1] is INamedTypeSymbol r)
                    {
                        return r;
                    }
                }
            }

            return null;
        }

        internal static ImmutableArray<IFieldSymbol> InstanceFields(INamedTypeSymbol type)
        {
            var b = ImmutableArray.CreateBuilder<IFieldSymbol>();
            foreach (var m in type.GetMembers())
            {
                if (m is IFieldSymbol f && !f.IsStatic && f.DeclaredAccessibility == Accessibility.Public)
                {
                    b.Add(f);
                }
            }

            return b.ToImmutable();
        }

        internal static bool HasGraphPort(IFieldSymbol field, INamedTypeSymbol graphPortAttr)
        {
            foreach (var a in field.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphPortAttr))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class TypeFmt
    {
        internal static bool IsInt(ITypeSymbol t) => t.SpecialType == SpecialType.System_Int32;

        internal static bool IsString(ITypeSymbol t) => t.SpecialType == SpecialType.System_String;

        internal static string Simple(ITypeSymbol t)
        {
            if (IsInt(t))
            {
                return "int";
            }

            if (IsString(t))
            {
                return "string";
            }

            return t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        internal static string DefaultLiteral(ITypeSymbol t)
        {
            if (IsInt(t))
            {
                return "0";
            }

            if (IsString(t))
            {
                return "\"\"";
            }

            return "default!";
        }
    }
}

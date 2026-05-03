using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class GraphPayloadNodeEmitter
    {
        internal static void Emit(SourceProductionContext spc, Compilation compilation, bool editorAssembly, GraphPackageModel package, CancellationToken ct)
        {
            var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, package.RunnerFullyQualified);
            if (runner == null)
            {
                return;
            }

            // Marker interfaces live in the same namespace as the runner's GraphRunner base (e.g. M0 uses Scaffold.GraphFlow.M0).
            var markerNs = string.IsNullOrEmpty(package.GraphFrameworkNamespace) ? "Scaffold.GraphFlow" : package.GraphFrameworkNamespace;
            var iEntry = compilation.GetTypeByMetadataName(markerNs + ".IGraphEntry`1");
            var iAction = compilation.GetTypeByMetadataName(markerNs + ".IGraphAction`1");
            var iExec = compilation.GetTypeByMetadataName(markerNs + ".IExecutable`1");
            var graphEntryAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphEntryAttribute");
            var graphCmdAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphCommandPairAttribute");
            var graphPortAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphPortAttribute");
            if (iEntry == null || iAction == null || graphEntryAttr == null || graphPortAttr == null)
            {
                return;
            }

            var entryIface = iEntry.Construct(runner);
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

                if (PayloadDiscovery.Implements(type, entryIface))
                {
                    if (!PayloadDiscovery.TryGetGraphEntryFlowOut(type, graphEntryAttr, out var flowOut))
                    {
                        continue;
                    }

                    if (editorAssembly)
                    {
                        EmitEntryEditor(spc, type, compilation);
                    }
                    else
                    {
                        EmitEntryRuntime(spc, type, package.RunnerTypeName, flowOut, graphPortAttr);
                    }

                    continue;
                }

                // Mode 2 command/result pairs must win over IExecutable-shaped emission so DispatcherBase (partial + overrides) is consistent.
                if (graphCmdAttr != null &&
                    PayloadDiscovery.TryReadCommandPairAttribute(type, graphCmdAttr, out var resultType, out var fi, out var fo))
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

                    // typeof(...) args in attributes serialized into a referenced assembly's metadata can come back Kind=Error
                    // because the C# compiler omits the assembly qualifier for same-assembly type names. When that happens,
                    // recover the result type by finding a concrete dispatcher subclass closed over this command.
                    if (resultType == null)
                    {
                        resultType = PayloadDiscovery.FindResultTypeFromDispatcherSubclass(payloadAssembly, openNamed, type, ct);
                        if (resultType == null)
                        {
                            continue;
                        }
                    }

                    var closedDisp = openNamed.Construct(type, resultType);
                    if (editorAssembly)
                    {
                        EmitCommandEditor(spc, type, resultType, fi, fo, compilation);
                    }
                    else
                    {
                        EmitCommandRuntime(
                            spc,
                            type,
                            resultType,
                            fi,
                            fo,
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
                    if (editorAssembly)
                    {
                        EmitExecutableEditor(spc, type, compilation);
                    }
                    else
                    {
                        EmitExecutableRuntime(spc, type, package.RunnerTypeName, graphPortAttr);
                    }
                }
            }
        }

        static void EmitEntryEditor(SourceProductionContext spc, INamedTypeSymbol payload, Compilation compilation)
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
            sb.AppendLine($"    public sealed class {leaf}EditorNode : Node");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const string FlowOutPortName = \"FlowOut\";");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
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
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
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

        static void EmitEntryRuntime(SourceProductionContext spc, INamedTypeSymbol payload, string runnerName, int flowOut, INamedTypeSymbol graphPortAttr)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Scaffold.GraphFlow.M0;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed class {leaf}Runtime : EntryRuntimeNode<{leaf}, {runnerName}>");
            sb.AppendLine("    {");
            sb.AppendLine("        public static class Ports");
            sb.AppendLine("        {");
            sb.AppendLine($"            public const int FlowOut = {PortIdLiteral(flowOut)};");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (!PayloadDiscovery.TryGetGraphPortId(f, graphPortAttr, out var pid))
                {
                    continue;
                }

                sb.AppendLine($"            public const int {f.Name} = {PortIdLiteral(pid)};");
            }

            sb.AppendLine("        }");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine();
                sb.AppendLine($"        [NonSerialized] public {kt} _out_{f.Name};");
            }

            sb.AppendLine();
            sb.AppendLine("        public override Connection GetOutputConnection(int portId) => portId switch");
            sb.AppendLine("        {");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (!PayloadDiscovery.TryGetGraphPortId(f, graphPortAttr, out var pid))
                {
                    continue;
                }

                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine($"            Ports.{f.Name} => new Connection<{kt}>(this, Ports.{f.Name}, () => _out_{f.Name}),");
            }

            sb.AppendLine("            _ => throw new ArgumentOutOfRangeException(nameof(portId)),");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        public override void BindInput(int portId, Connection connection) =>");
            sb.AppendLine("            throw new ArgumentOutOfRangeException(nameof(portId));");
            sb.AppendLine();
            sb.AppendLine($"        public override Task<FlowContinuation> Execute({runnerName} runner)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Payload != null)");
            sb.AppendLine("            {");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                sb.AppendLine($"                _out_{f.Name} = Payload.{f.Name};");
            }

            sb.AppendLine("            }");
            sb.AppendLine("            return Task.FromResult(FlowContinuation.Next(Ports.FlowOut));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}Runtime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static string PortIdLiteral(int pid) => "unchecked((int)0x" + ((uint)pid).ToString("X8") + "u)";

        static void EmitExecutableEditor(SourceProductionContext spc, INamedTypeSymbol payload, Compilation compilation)
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
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
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
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
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

        static void EmitExecutableRuntime(SourceProductionContext spc, INamedTypeSymbol payload, string runnerName, INamedTypeSymbol graphPortAttr)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Scaffold.GraphFlow.M0;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed class {leaf}DispatcherRuntime : RuntimeNode<{runnerName}>");
            sb.AppendLine("    {");
            sb.AppendLine("        public const int FlowInSlotId = 0;");
            sb.AppendLine();
            sb.AppendLine("        public static class Ports");
            sb.AppendLine("        {");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (!PayloadDiscovery.TryGetGraphPortId(f, graphPortAttr, out var pid))
                {
                    continue;
                }

                sb.AppendLine($"            public const int {f.Name} = {PortIdLiteral(pid)};");
            }

            sb.AppendLine("        }");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine();
                sb.AppendLine($"        public {kt} {f.Name} = {TypeFmt.DefaultLiteral(f.Type)};");
                sb.AppendLine();
                sb.AppendLine($"        [NonSerialized] public Connection<{kt}>? _in_{f.Name};");
            }

            sb.AppendLine();
            sb.AppendLine("        public override Connection GetOutputConnection(int portId) =>");
            sb.AppendLine("            throw new ArgumentOutOfRangeException(nameof(portId));");
            sb.AppendLine();
            sb.AppendLine("        public override void BindInput(int portId, Connection connection)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (portId)");
            sb.AppendLine("            {");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (!PayloadDiscovery.TryGetGraphPortId(f, graphPortAttr, out _))
                {
                    continue;
                }

                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine($"                case Ports.{f.Name}:");
                sb.AppendLine($"                    _in_{f.Name} = (Connection<{kt}>)connection;");
                sb.AppendLine("                    return;");
            }

            sb.AppendLine("                default:");
            sb.AppendLine("                    throw new ArgumentOutOfRangeException(nameof(portId));");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override async Task<FlowContinuation> Execute({runnerName} runner)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var payload = new {leaf}");
            sb.AppendLine("            {");
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine($"                {f.Name} = _in_{f.Name} != null ? _in_{f.Name}.Read() : {f.Name},");
            }

            sb.AppendLine("            };");
            sb.AppendLine("            await payload.Execute(runner).ConfigureAwait(false);");
            sb.AppendLine("            return FlowContinuation.Stop;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}DispatcherRuntime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static void EmitCommandEditor(
            SourceProductionContext spc,
            INamedTypeSymbol cmd,
            INamedTypeSymbol result,
            int flowIn,
            int flowOut,
            Compilation compilation)
        {
            var sb = new StringBuilder();
            var leaf = cmd.Name;
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
            sb.AppendLine($"        public const string FlowOutPortName = \"FlowOut\";");
            foreach (var f in PayloadDiscovery.InstanceFields(cmd))
            {
                sb.AppendLine($"        public const string {f.Name}PortName = \"{f.Name}\";");
            }

            foreach (var f in PayloadDiscovery.InstanceFields(result))
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
            foreach (var f in PayloadDiscovery.InstanceFields(cmd))
            {
                var gm = EditorGtInputPort(f.Type);
                sb.AppendLine($"            context.AddInputPort{gm}({f.Name}PortName).Build();");
            }

            foreach (var f in PayloadDiscovery.InstanceFields(result))
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
            int flowIn,
            int flowOut,
            string closedDispatcherFq,
            string runnerName,
            INamedTypeSymbol graphPortAttr)
        {
            var sb = new StringBuilder();
            var leaf = cmd.Name;
            var typeNs = cmd.ContainingNamespace.IsGlobalNamespace ? "" : cmd.ContainingNamespace.ToDisplayString();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Scaffold.GraphFlow.M0;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed partial class {leaf}DispatcherRuntime : {closedDispatcherFq}");
            sb.AppendLine("    {");
            sb.AppendLine("        public static class Ports");
            sb.AppendLine("        {");
            sb.AppendLine($"            public const int FlowIn = {PortIdLiteral(flowIn)};");
            sb.AppendLine($"            public const int FlowOut = {PortIdLiteral(flowOut)};");
            foreach (var f in PayloadDiscovery.InstanceFields(cmd))
            {
                if (!PayloadDiscovery.TryGetGraphPortId(f, graphPortAttr, out var pid))
                {
                    continue;
                }

                sb.AppendLine($"            public const int {f.Name} = {PortIdLiteral(pid)};");
            }

            foreach (var f in PayloadDiscovery.InstanceFields(result))
            {
                if (!PayloadDiscovery.TryGetGraphPortId(f, graphPortAttr, out var pid))
                {
                    continue;
                }

                sb.AppendLine($"            public const int {f.Name} = {PortIdLiteral(pid)};");
            }

            sb.AppendLine("        }");
            foreach (var f in PayloadDiscovery.InstanceFields(cmd))
            {
                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine();
                sb.AppendLine($"        public {kt} {f.Name};");
                sb.AppendLine();
                sb.AppendLine($"        [NonSerialized] Connection<{kt}>? _in_{f.Name};");
            }

            foreach (var f in PayloadDiscovery.InstanceFields(result))
            {
                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine();
                sb.AppendLine($"        [NonSerialized] public {kt} _out_{f.Name} = {TypeFmt.DefaultLiteral(f.Type)};");
            }

            sb.AppendLine();
            sb.AppendLine("        protected override int FlowOutPortId => Ports.FlowOut;");
            sb.AppendLine();
            sb.AppendLine($"        protected override {cmd.Name} BuildPayload() => new {cmd.Name} {{ {BuildPayloadObjectInitializer(cmd)} }};");
            sb.AppendLine();
            AppendWriteOutputsBody(sb, result);
            sb.AppendLine();
            sb.AppendLine("        public override Connection GetOutputConnection(int portId) => portId switch");
            sb.AppendLine("        {");
            foreach (var f in PayloadDiscovery.InstanceFields(result))
            {
                if (!PayloadDiscovery.TryGetGraphPortId(f, graphPortAttr, out _))
                {
                    continue;
                }

                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine($"            Ports.{f.Name} => new Connection<{kt}>(this, Ports.{f.Name}, () => _out_{f.Name}),");
            }

            sb.AppendLine("            _ => throw new ArgumentOutOfRangeException(nameof(portId)),");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        public override void BindInput(int portId, Connection connection)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (portId)");
            sb.AppendLine("            {");
            foreach (var f in PayloadDiscovery.InstanceFields(cmd))
            {
                if (!PayloadDiscovery.TryGetGraphPortId(f, graphPortAttr, out _))
                {
                    continue;
                }

                var kt = TypeFmt.Simple(f.Type);
                sb.AppendLine($"                case Ports.{f.Name}:");
                sb.AppendLine($"                    _in_{f.Name} = (Connection<{kt}>)connection;");
                sb.AppendLine("                    return;");
            }

            sb.AppendLine("                default:");
            sb.AppendLine("                    throw new ArgumentOutOfRangeException(nameof(portId));");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}DispatcherRuntime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static void AppendWriteOutputsBody(StringBuilder sb, INamedTypeSymbol result)
        {
            var fields = PayloadDiscovery.InstanceFields(result);
            if (fields.Length == 1)
            {
                var f = fields[0];
                sb.AppendLine($"        protected override void WriteOutputs({result.Name} result) => _out_{f.Name} = result.{f.Name};");
                return;
            }

            sb.AppendLine($"        protected override void WriteOutputs({result.Name} result)");
            sb.AppendLine("        {");
            foreach (var f in fields)
            {
                sb.AppendLine($"            _out_{f.Name} = result.{f.Name};");
            }

            sb.AppendLine("        }");
        }

        static string BuildPayloadObjectInitializer(INamedTypeSymbol cmd)
        {
            var sb = new StringBuilder();
            foreach (var f in PayloadDiscovery.InstanceFields(cmd))
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"{f.Name} = _in_{f.Name} != null ? _in_{f.Name}.Read() : {f.Name}");
            }

            return sb.ToString();
        }
    }

    internal static class PayloadDiscovery
    {
        internal static bool IsCandidateType(INamedTypeSymbol type) =>
            (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) && type.DeclaredAccessibility == Accessibility.Public;

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

        internal static bool TryGetGraphEntryFlowOut(INamedTypeSymbol type, INamedTypeSymbol graphEntryAttr, out int flowOut)
        {
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphEntryAttr))
                {
                    continue;
                }

                foreach (var na in a.NamedArguments)
                {
                    if (na.Key == "FlowOutPortId" && na.Value.Value is int i)
                    {
                        flowOut = i;
                        return true;
                    }
                }
            }

            flowOut = 0;
            return false;
        }

        internal static bool TryReadCommandPairAttribute(
            INamedTypeSymbol type,
            INamedTypeSymbol graphCmdAttr,
            out INamedTypeSymbol? resultType,
            out int flowIn,
            out int flowOut)
        {
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphCmdAttr))
                {
                    continue;
                }

                resultType = null;
                flowIn = 0;
                flowOut = 0;
                foreach (var na in a.NamedArguments)
                {
                    if (na.Key == "ResultType" && na.Value.Value is INamedTypeSymbol r)
                    {
                        resultType = r;
                    }
                    else if (na.Key == "FlowInPortId" && na.Value.Value is int fi)
                    {
                        flowIn = fi;
                    }
                    else if (na.Key == "FlowOutPortId" && na.Value.Value is int fo)
                    {
                        flowOut = fo;
                    }
                }

                return true;
            }

            resultType = null;
            flowIn = 0;
            flowOut = 0;
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

        internal static bool TryGetGraphPortId(IFieldSymbol field, INamedTypeSymbol graphPortAttr, out int id)
        {
            foreach (var a in field.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphPortAttr))
                {
                    continue;
                }

                foreach (var na in a.NamedArguments)
                {
                    if (na.Key == "Id" && na.Value.Value is int i)
                    {
                        id = i;
                        return true;
                    }
                }
            }

            id = 0;
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

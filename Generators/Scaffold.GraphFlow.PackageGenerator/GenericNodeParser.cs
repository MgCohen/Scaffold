using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>
    /// Walks an assembly for hand-written <c>RuntimeNode&lt;TRunner&gt;</c> classes annotated with <c>[GraphNode]</c>
    /// and projects them into <see cref="GenericNodeModel"/> records the editor-mirror emitter consumes.
    /// </summary>
    internal static class GenericNodeParser
    {
        const string GraphNodeAttrFqn = "Scaffold.GraphFlow.GraphNodeAttribute";
        const string FlowInAttrFqn = "Scaffold.GraphFlow.FlowInAttribute";
        const string FlowOutAttrFqn = "Scaffold.GraphFlow.FlowOutAttribute";
        const string InputAttrFqn = "Scaffold.GraphFlow.InputAttribute";
        const string OutputAttrFqn = "Scaffold.GraphFlow.OutputAttribute";

        internal static ImmutableArray<GenericNodeModel> Parse(Compilation compilation, IAssemblySymbol assembly, CancellationToken ct)
        {
            var graphNodeAttr = compilation.GetTypeByMetadataName(GraphNodeAttrFqn);
            var flowInAttr = compilation.GetTypeByMetadataName(FlowInAttrFqn);
            var flowOutAttr = compilation.GetTypeByMetadataName(FlowOutAttrFqn);
            var inputAttr = compilation.GetTypeByMetadataName(InputAttrFqn);
            var outputAttr = compilation.GetTypeByMetadataName(OutputAttrFqn);
            if (graphNodeAttr == null || flowInAttr == null || flowOutAttr == null || inputAttr == null || outputAttr == null)
            {
                return ImmutableArray<GenericNodeModel>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<GenericNodeModel>();
            foreach (var type in GraphPayloadTypeWalker.AllNamedTypesInAssembly(assembly, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (!IsCandidateGenericNode(type))
                {
                    continue;
                }

                if (!TryParseModel(type, graphNodeAttr, flowInAttr, flowOutAttr, inputAttr, outputAttr, out var model))
                {
                    continue;
                }

                builder.Add(model);
            }

            return builder.ToImmutable();
        }

        static bool IsCandidateGenericNode(INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract)
            {
                return false;
            }

            if (type.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            // Must derive from RuntimeNode<TRunner> (directly or transitively).
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.MetadataName == "RuntimeNode`1")
                {
                    return true;
                }
            }

            return false;
        }

        static bool HasGraphNodeAttribute(INamedTypeSymbol type, INamedTypeSymbol graphNodeAttr, out string? category)
        {
            category = null;
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphNodeAttr))
                {
                    continue;
                }

                foreach (var na in a.NamedArguments)
                {
                    if (na.Key == "Category" && na.Value.Value is string s)
                    {
                        category = s;
                    }
                }

                return true;
            }

            return false;
        }

        static bool TryParseModel(
            INamedTypeSymbol type,
            INamedTypeSymbol graphNodeAttr,
            INamedTypeSymbol flowInAttr,
            INamedTypeSymbol flowOutAttr,
            INamedTypeSymbol inputAttr,
            INamedTypeSymbol outputAttr,
            out GenericNodeModel model)
        {
            model = default;
            if (!HasGraphNodeAttribute(type, graphNodeAttr, out var category))
            {
                return false;
            }

            // M2 supports either non-generic nodes or single-type-param nodes whose param is the TRunner of RuntimeNode<TRunner>.
            // Multi-type-param nodes (Equals<T>, IsOfType<T>) are deferred — they need per-T closed instantiations the consumer registers explicitly.
            var isGenericOverRunner = type.TypeParameters.Length == 1;
            if (type.TypeParameters.Length > 1)
            {
                return false;
            }

            var flowIns = ParseFlowPorts(type, flowInAttr);
            var flowOuts = ParseFlowPorts(type, flowOutAttr);
            var inputs = ParseDataPorts(type, inputAttr, expectConnectionWrapper: true);
            var outputs = ParseDataPorts(type, outputAttr, expectConnectionWrapper: false);

            var ns = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
            model = new GenericNodeModel(ns, type.Name, isGenericOverRunner, category, flowIns, flowOuts, inputs, outputs);
            return true;
        }

        static ImmutableArray<GenericNodeFlowPort> ParseFlowPorts(INamedTypeSymbol type, INamedTypeSymbol flowAttrSymbol)
        {
            var builder = ImmutableArray.CreateBuilder<GenericNodeFlowPort>();
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, flowAttrSymbol))
                {
                    continue;
                }

                if (a.ConstructorArguments.Length == 0 || a.ConstructorArguments[0].Value is not int portId)
                {
                    continue;
                }

                var name = a.ConstructorArguments.Length > 1 && a.ConstructorArguments[1].Value is string s ? s : DefaultFlowName(flowAttrSymbol);
                builder.Add(new GenericNodeFlowPort(name, portId));
            }

            return builder.ToImmutable();
        }

        static string DefaultFlowName(INamedTypeSymbol flowAttrSymbol) =>
            flowAttrSymbol.Name == "FlowInAttribute" ? "FlowIn" : "FlowOut";

        static ImmutableArray<GenericNodeDataPort> ParseDataPorts(INamedTypeSymbol type, INamedTypeSymbol dataAttrSymbol, bool expectConnectionWrapper)
        {
            var builder = ImmutableArray.CreateBuilder<GenericNodeDataPort>();
            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol field || field.IsStatic)
                {
                    continue;
                }

                int? portId = null;
                foreach (var a in field.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, dataAttrSymbol))
                    {
                        continue;
                    }

                    if (a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is int pid)
                    {
                        portId = pid;
                        break;
                    }
                }

                if (portId is null)
                {
                    continue;
                }

                var inner = expectConnectionWrapper
                    ? UnwrapConnection(field.Type) ?? field.Type
                    : field.Type;

                builder.Add(new GenericNodeDataPort(field.Name, portId.Value, TypeFmt.Simple(inner)));
            }

            return builder.ToImmutable();
        }

        /// <summary>Unwraps <c>Connection&lt;T&gt;</c> (with or without a trailing nullable annotation) and returns <c>T</c>.</summary>
        static ITypeSymbol? UnwrapConnection(ITypeSymbol fieldType)
        {
            // Strip the nullable annotation; the underlying named type is what carries the type argument.
            if (fieldType is INamedTypeSymbol named &&
                named.Name == "Connection" &&
                named.TypeArguments.Length == 1)
            {
                return named.TypeArguments[0];
            }

            return null;
        }
    }
}

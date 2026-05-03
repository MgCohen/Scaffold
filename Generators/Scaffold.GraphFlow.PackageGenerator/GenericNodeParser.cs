using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>
    /// Walks an assembly for hand-written <c>[GraphNode]</c> classes and projects them into
    /// <see cref="GenericNodeModel"/> records the <see cref="GraphGenericNodeEmitter"/> consumes.
    ///
    /// <para>Single TRunner type parameter (or none for pure data nodes); multi-T cases (Equals&lt;T&gt;)
    /// are deferred to M4 — see EFG011 in <see cref="Diagnostics"/>.</para>
    /// </summary>
    internal static class GenericNodeParser
    {
        const string GraphNodeAttrFqn = "Scaffold.GraphFlow.GraphNodeAttribute";
        const string GraphPortAttrFqn = "Scaffold.GraphFlow.GraphPortAttribute";

        /// <summary>Implicit FlowIn port id assigned to every flow-bearing <c>[GraphNode]</c>.</summary>
        internal const int ImplicitFlowInPortId = 0;

        internal static ImmutableArray<GenericNodeModel> Parse(Compilation compilation, IAssemblySymbol assembly, CancellationToken ct)
        {
            var graphNodeAttr = compilation.GetTypeByMetadataName(GraphNodeAttrFqn);
            var graphPortAttr = compilation.GetTypeByMetadataName(GraphPortAttrFqn);
            if (graphNodeAttr == null)
            {
                return ImmutableArray<GenericNodeModel>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<GenericNodeModel>();
            foreach (var type in GraphPayloadTypeWalker.AllNamedTypesInAssembly(assembly, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (!HasGraphNodeAttribute(type, graphNodeAttr, out var category))
                {
                    continue;
                }

                if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (type.TypeParameters.Length > 1)
                {
                    // EFG011 — multi-T deferred to M4. Skip emission silently for now.
                    continue;
                }

                var (isFlowNode, isGenericOverRunner) = ClassifyHierarchy(type);
                if (!isFlowNode && !DerivesFromRuntimeNodeBase(type))
                {
                    continue;
                }

                var (inputs, outputs, flowOuts) = ParseFields(type, graphPortAttr);
                var hasInitHook = HasInitializePortsHook(type);

                var ns = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
                builder.Add(new GenericNodeModel(
                    ns, type.Name, isFlowNode, isGenericOverRunner,
                    category, ImplicitFlowInPortId, flowOuts, inputs, outputs, hasInitHook));
            }

            return builder.ToImmutable();
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

        /// <summary>Walks the base chain. Returns (isFlowNode, isGenericOverRunner) — flow nodes derive from <c>RuntimeNode`1</c>, data nodes derive from <c>RuntimeNode</c> directly.</summary>
        static (bool isFlowNode, bool isGenericOverRunner) ClassifyHierarchy(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.MetadataName == "RuntimeNode`1")
                {
                    // Flow node. The current concrete type is generic over TRunner if it has a type parameter.
                    return (true, type.TypeParameters.Length == 1);
                }
            }

            return (false, false);
        }

        static bool DerivesFromRuntimeNodeBase(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.Name == "RuntimeNode" && !current.IsGenericType)
                {
                    return true;
                }
            }

            return false;
        }

        static (ImmutableArray<GenericNodeInputField> inputs,
                ImmutableArray<GenericNodeOutputField> outputs,
                ImmutableArray<GenericNodeFlowOut> flowOuts) ParseFields(INamedTypeSymbol type, INamedTypeSymbol? graphPortAttr)
        {
            var inputs = ImmutableArray.CreateBuilder<GenericNodeInputField>();
            var outputs = ImmutableArray.CreateBuilder<GenericNodeOutputField>();
            var flowOuts = ImmutableArray.CreateBuilder<GenericNodeFlowOut>();
            int nextSequential = 1;

            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol field || field.IsStatic)
                {
                    continue;
                }

                if (field.Type is not INamedTypeSymbol typed)
                {
                    continue;
                }

                int portId = TryGetExplicitPortId(field, graphPortAttr) ?? nextSequential++;

                if (typed.Name == "InputPort" && typed.TypeArguments.Length == 1)
                {
                    inputs.Add(new GenericNodeInputField(field.Name, portId, TypeFmt.Simple(typed.TypeArguments[0])));
                }
                else if (typed.Name == "OutputPort" && typed.TypeArguments.Length == 1)
                {
                    outputs.Add(new GenericNodeOutputField(field.Name, portId, TypeFmt.Simple(typed.TypeArguments[0])));
                }
                else if (typed.Name == "FlowOut")
                {
                    flowOuts.Add(new GenericNodeFlowOut(field.Name, portId));
                }
            }

            return (inputs.ToImmutable(), outputs.ToImmutable(), flowOuts.ToImmutable());
        }

        static int? TryGetExplicitPortId(IFieldSymbol field, INamedTypeSymbol? graphPortAttr)
        {
            if (graphPortAttr == null) return null;
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
                        return i;
                    }
                }
            }

            return null;
        }

        /// <summary>True if the class declares <c>InitializePorts()</c> as a partial method (with or without body).</summary>
        static bool HasInitializePortsHook(INamedTypeSymbol type)
        {
            foreach (var member in type.GetMembers("InitializePorts"))
            {
                if (member is IMethodSymbol m && m.IsPartialDefinition)
                {
                    return true;
                }
                if (member is IMethodSymbol m2 && m2.PartialImplementationPart != null)
                {
                    return true;
                }
            }

            // Fallback: presence of any method named InitializePorts on the type triggers the hook
            // (covers cases where the symbol model doesn't expose IsPartialDefinition cleanly).
            foreach (var member in type.GetMembers("InitializePorts"))
            {
                if (member is IMethodSymbol)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

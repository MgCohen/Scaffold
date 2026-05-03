using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    static class GraphPackageAssemblyParser
    {
        internal static ImmutableArray<GraphPackageModel> ParsePackages(Compilation compilation, INamedTypeSymbol? graphPackageAttrType, CancellationToken ct)
        {
            var builder = ImmutableArray.CreateBuilder<GraphPackageModel>();
            if (graphPackageAttrType == null)
            {
                return builder.ToImmutable();
            }

            foreach (var attr in compilation.Assembly.GetAttributes())
            {
                ct.ThrowIfCancellationRequested();
                if (TryCreateModel(attr, graphPackageAttrType, out var model))
                {
                    builder.Add(model);
                }
            }

            return builder.ToImmutable();
        }

        static bool TryCreateModel(AttributeData attr, INamedTypeSymbol graphPackageAttrType, out GraphPackageModel model)
        {
            model = default;
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, graphPackageAttrType))
            {
                return false;
            }

            var runner = AttributeNamedArguments.TryGetNamedType(attr, "Runner");
            if (runner == null)
            {
                return false;
            }

            ReadDisplayAndStrings(attr, runner, out model);
            return true;
        }

        static void ReadDisplayAndStrings(AttributeData attr, INamedTypeSymbol runner, out GraphPackageModel model)
        {
            var fq = runner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var ns = runner.ContainingNamespace.IsGlobalNamespace ? "" : runner.ContainingNamespace.ToDisplayString();
            var stem = RunnerStem.FromTypeName(runner.Name);
            AttributeNamedArguments.ReadPackageStrings(attr, out var ext, out var menu, out var reg);
            var frameworkNs = GraphFrameworkNamespaceFromRunner(runner);
            var dispatcherMeta = AttributeNamedArguments.TryGetNamedType(attr, "DispatcherBase") is { } dBase
                ? (dBase.ContainingNamespace.IsGlobalNamespace
                    ? ""
                    : dBase.ContainingNamespace.ToDisplayString() + ".") + dBase.MetadataName
                : null;
            model = new GraphPackageModel(fq, ns, runner.Name, stem, ext, menu, reg, frameworkNs, dispatcherMeta);
        }

        static string GraphFrameworkNamespaceFromRunner(INamedTypeSymbol runner)
        {
            for (var current = runner.BaseType; current != null; current = current.BaseType)
            {
                if (current.Name != "GraphRunner")
                {
                    continue;
                }

                return current.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : current.ContainingNamespace.ToDisplayString();
            }

            return string.Empty;
        }
    }
}

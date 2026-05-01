using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class LiveOpsKeyAnalyzers : DiagnosticAnalyzer
    {
        public const string SCA7101 = "SCA7101";
        public const string SCA7102 = "SCA7102";
        public const string SCA7103 = "SCA7103";
        private const string Category = "Hygiene";

        private static readonly DiagnosticDescriptor RuleMissingAttribute = new(
            SCA7101,
            "LiveOps: missing [LiveOpsKey] on module DTO or request",
            "SCA7101: type '{0}' should declare [LiveOpsKey] (concrete IGameModuleData or type inheriting from ModuleRequest).",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor RuleRawCacheKey = new(
            SCA7102,
            "LiveOps: avoid string literal for data-cache key",
            "SCA7102: do not use a string literal for the cache 'key' parameter on IReadableDataCache/IWriteableDataCache; use KeyOf<T>.Module and extension methods in DataCacheExtensions when possible.",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor RuleRawRequestKey = new(
            SCA7103,
            "LiveOps: avoid string literal for GameApi RequestKey",
            "SCA7103: do not use a string literal for GameApiEnvelopeRequest.RequestKey; use KeyOf.WireOf(request) or a generated constant from LiveOps.Keys.Generated.LiveOpsKeys where applicable.",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(RuleMissingAttribute, RuleRawCacheKey, RuleRawRequestKey);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeTypeSymbol, SymbolKind.NamedType);
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        }

        private static void AnalyzeTypeSymbol(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol named)
            {
                return;
            }

            if (named.TypeKind is not (TypeKind.Class or TypeKind.Struct))
            {
                return;
            }

            if (named.IsAbstract || named.IsStatic)
            {
                return;
            }

            if (!InLiveOpsDtos(named))
            {
                return;
            }

            if (!RequiresKeyAttribute(named, context.Compilation, out int reason))
            {
                return;
            }

            if (HasAttribute(named, "LiveOps.DTO.Keys.LiveOpsKeyAttribute", context.Compilation))
            {
                return;
            }

            string label = reason == 0 ? "IGameModuleData" : "ModuleRequest";
            Location? loc = named.Locations.IsDefaultOrEmpty ? Location.None : named.Locations[0];
            context.ReportDiagnostic(
                Diagnostic.Create(
                    RuleMissingAttribute,
                    loc,
                    new object[] { named.Name + " (" + label + ")" }));
        }

        private static bool InLiveOpsDtos(INamedTypeSymbol type)
        {
            string? ns = type.ContainingNamespace?.ToDisplayString();
            if (string.IsNullOrEmpty(ns))
            {
                return false;
            }

            return ns.StartsWith("LiveOps.Modules.DTO", StringComparison.Ordinal) ||
                ns.StartsWith("LiveOps.DTO.GameModule", StringComparison.Ordinal);
        }

        private static bool RequiresKeyAttribute(
            INamedTypeSymbol type,
            Compilation compilation,
            out int reason)
        {
            reason = 0;
            INamedTypeSymbol? igma = compilation.GetTypeByMetadataName("LiveOps.DTO.GameModule.IGameModuleData");
            if (igma is not null && ImplementsInterface(type, igma) && type.TypeKind is not TypeKind.Interface)
            {
                return true;
            }

            if (InheritsFromModuleRequestInNamespace(type))
            {
                reason = 1;
                return true;
            }

            return false;
        }

        private static bool InheritsFromModuleRequestInNamespace(INamedTypeSymbol type)
        {
            INamedTypeSymbol? b = type.BaseType;
            while (b is not null)
            {
                if (b.Name == "ModuleRequest" &&
                    string.Equals(
                        b.ContainingNamespace?.ToDisplayString(),
                        "LiveOps.DTO.ModuleRequest",
                        StringComparison.Ordinal))
                {
                    return true;
                }

                b = b.BaseType;
            }

            return false;
        }

        private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol iface)
        {
            if (type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iface.OriginalDefinition) ||
                SymbolEqualityComparer.Default.Equals(i, iface)))
            {
                return true;
            }

            return type.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iface.OriginalDefinition));
        }

        private static bool HasAttribute(INamedTypeSymbol type, string metadataName, Compilation compilation)
        {
            INamedTypeSymbol? attr = compilation.GetTypeByMetadataName(metadataName);
            if (attr is null)
            {
                return false;
            }

            return type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attr));
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            if (IsTestOrGenerated(context.Operation.Syntax.SyntaxTree.FilePath))
            {
                return;
            }

            if (context.Operation is not IInvocationOperation inv)
            {
                return;
            }

            IMethodSymbol? m = inv.TargetMethod;
            if (m is null)
            {
                return;
            }

            IMethodSymbol def = m.ReducedFrom ?? m.OriginalDefinition;
            if (def.Name is not "Get" and not "Set" and not "Exists" and not "Delete")
            {
                return;
            }

            INamedTypeSymbol? tr = m.ReceiverType as INamedTypeSymbol;
            if (tr is null)
            {
                return;
            }

            ITypeSymbol? readable = context.Compilation.GetTypeByMetadataName("LiveOps.ModuleFetchData.IReadableDataCache");
            ITypeSymbol? writeable = context.Compilation.GetTypeByMetadataName("LiveOps.ModuleFetchData.IWriteableDataCache");
            if (readable is null && writeable is null)
            {
                return;
            }

            ITypeSymbol? rec = m.ReducedFrom is not null && inv.Instance is not null
                ? (inv.Instance.Type as ITypeSymbol) ?? tr
                : tr;
            if (rec is null)
            {
                return;
            }

            if (!TypeImplementsDataCacheInterface(rec, readable, writeable))
            {
                return;
            }

            foreach (IArgumentOperation arg in inv.Arguments)
            {
                IParameterSymbol? p = arg.Parameter;
                if (p is null || p.Name != "key" || p.Type.SpecialType != SpecialType.System_String)
                {
                    continue;
                }

                if (arg.Value is ILiteralOperation lit && lit.ConstantValue.HasValue && lit.Type?.SpecialType == SpecialType.System_String)
                {
                    context.ReportDiagnostic(Diagnostic.Create(RuleRawCacheKey, arg.Syntax.GetLocation(), Array.Empty<object>()));
                }
            }
        }

        private static bool TypeImplementsDataCacheInterface(ITypeSymbol rec, ITypeSymbol? readable, ITypeSymbol? writeable)
        {
            ITypeSymbol? t = rec;
            while (t is not null)
            {
                if (readable is not null)
                {
                    if (t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, readable)))
                    {
                        return true;
                    }
                }

                if (writeable is not null)
                {
                    if (t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, writeable)))
                    {
                        return true;
                    }
                }

                t = t.BaseType;
            }

            return false;
        }

        private static void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            if (IsTestOrGenerated(context.Operation.Syntax.SyntaxTree.FilePath))
            {
                return;
            }

            if (context.Operation is not IObjectCreationOperation o)
            {
                return;
            }

            ITypeSymbol? t = o.Type;
            if (t is null || t.Name != "GameApiEnvelopeRequest")
            {
                return;
            }

            if (o.Initializer is not IObjectOrCollectionInitializerOperation init)
            {
                return;
            }

            foreach (IOperation op in init.Initializers)
            {
                if (op is not IAssignmentOperation assign || assign.Target is not IPropertyReferenceOperation pr)
                {
                    continue;
                }

                if (pr.Property.Name != "RequestKey")
                {
                    continue;
                }

                if (assign.Value is ILiteralOperation lit && lit.Type?.SpecialType == SpecialType.System_String)
                {
                    string? s = lit.ConstantValue.Value as string;
                    if (string.IsNullOrEmpty(s))
                    {
                        continue;
                    }

                    if (string.Equals(s, "nope", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            RuleRawRequestKey,
                            assign.Syntax.GetLocation(),
                            Array.Empty<object>()));
                }
            }
        }

        private static bool IsTestOrGenerated(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return path.IndexOf("LiveOps\\Tests", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("LiveOps/Tests", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf(".Tests.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Scaffold.LiveOps.Tests", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("com.scaffold.liveops\\Tests", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

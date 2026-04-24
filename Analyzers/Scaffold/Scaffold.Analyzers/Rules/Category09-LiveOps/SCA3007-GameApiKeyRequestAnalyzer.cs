using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    /// <summary>Concrete <c>*Request</c> DTOs inheriting <c>ModuleRequest&lt;TResponse&gt;</c> in LiveOps DTO assemblies must use <c>[GameApiKey(…)]</c>.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class GameApiKeyRequestAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA3007";
        public const string Category = "Design";

        private static readonly LocalizableString Title = "Game API request DTOs must declare [GameApiKey]";

        private static readonly LocalizableString MessageFormat =
            "Request type '{0}' inherits ModuleRequest<…> but is missing [GameApiKey(…)] (used as GameApi wire key).";

        private static readonly LocalizableString Description =
            "Add [GameApiKey(\"YourKey\")] to the request DTO in LiveOps.DTO or LiveOps.Modules.DTO, matching the wire RequestKey.";

        public static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
            {
                return;
            }

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
        }

        private static void Analyze(SymbolAnalysisContext context)
        {
            INamedTypeSymbol? symbol = (INamedTypeSymbol)context.Symbol;
            if (symbol.ContainingAssembly is not { } asm)
            {
                return;
            }

            string? aName = asm.Name;
            if (aName is not ("LiveOps.DTO" or "LiveOps.Modules.DTO"))
            {
                return;
            }

            if (symbol.IsAbstract || !symbol.Name.EndsWith("Request", StringComparison.Ordinal) ||
                symbol.DeclaredAccessibility != Accessibility.Public)
            {
                return;
            }

            if (!InheritsFromModuleRequest(context.Compilation, symbol))
            {
                return;
            }

            bool has = false;
            foreach (AttributeData attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "GameApiKeyAttribute" || attr.AttributeClass?.Name is "GameApiKey")
                {
                    has = true;
                    break;
                }
            }

            if (!has)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Rule,
                        symbol.Locations[0],
                        symbol.Name));
            }
        }

        private static bool InheritsFromModuleRequest(Compilation compilation, INamedTypeSymbol type)
        {
            INamedTypeSymbol? mreq = compilation.GetTypeByMetadataName("LiveOps.DTO.ModuleRequest.ModuleRequest`1");
            if (mreq is null)
            {
                return false;
            }

            INamedTypeSymbol? t = type;
            while (t is not null)
            {
                if (t.OriginalDefinition.Equals(mreq, SymbolEqualityComparer.Default))
                {
                    return true;
                }

                t = t.BaseType;
            }

            return false;
        }
    }
}

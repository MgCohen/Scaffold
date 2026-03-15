using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MvvmBaseTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0015";
        private const string Category = "Architecture";
        private const string ViewModelInterface = "Scaffold.MVVM.IViewModel";
        private const string InpcInterface = "System.ComponentModel.INotifyPropertyChanged";
        private const string ViewModelBaseType = "Scaffold.MVVM.ViewModel";
        private const string ModelBaseType = "Scaffold.MVVM.Model";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "MVVM classes should use module base types",
            "Error SCA0015: Class '{0}' uses manual MVVM interfaces. Inherit from '{1}' and declare the class as partial to align with MVVM source generation conventions.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "MVVM classes should use Scaffold's Model/ViewModel base classes instead of manually implementing MVVM notifier interfaces.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Symbol.Locations[0].SourceTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            var typeSymbol = context.Symbol as INamedTypeSymbol;
            if (!IsCandidateClass(typeSymbol)) return;

            var location = typeSymbol.Locations.FirstOrDefault(locationItem => locationItem.IsInSource);
            if (location == null) return;

            if (ImplementsInterface(typeSymbol, ViewModelInterface) && !InheritsFrom(typeSymbol, ViewModelBaseType))
            {
                Report(context, rule, location, typeSymbol.Name, "ViewModel");
                return;
            }

            bool implementsInpc = ImplementsInterface(typeSymbol, InpcInterface);
            bool inheritsMvvmBase = InheritsFrom(typeSymbol, ViewModelBaseType) || InheritsFrom(typeSymbol, ModelBaseType);
            if (implementsInpc && !inheritsMvvmBase)
            {
                Report(context, rule, location, typeSymbol.Name, "Model/ViewModel");
            }
        }

        private static bool IsCandidateClass(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return false;
            if (typeSymbol.TypeKind != TypeKind.Class) return false;
            if (typeSymbol.Name == "ViewModel" || typeSymbol.Name == "Model") return false;

            var sourceLocation = typeSymbol.Locations.FirstOrDefault(locationItem => locationItem.IsInSource);
            if (sourceLocation == null) return false;

            var filePath = sourceLocation.SourceTree?.FilePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            var normalizedPath = filePath.Replace('\\', '/');
            if (normalizedPath.IndexOf("/Assets/Scripts/Core/MVVM/", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (normalizedPath.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (normalizedPath.IndexOf("/Samples/", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return true;
        }

        private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor rule, Location location, string className, string preferredBase)
        {
            var diagnostic = Diagnostic.Create(rule, location, className, preferredBase);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool ImplementsInterface(INamedTypeSymbol symbol, string interfaceName)
        {
            return symbol.AllInterfaces.Any(interfaceSymbol => interfaceSymbol.ToDisplayString() == interfaceName);
        }

        private static bool InheritsFrom(INamedTypeSymbol symbol, string baseTypeName)
        {
            var baseType = symbol.BaseType;
            while (baseType != null)
            {
                if (baseType.ToDisplayString() == baseTypeName)
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
            return false;
        }
    }
}

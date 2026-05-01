using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Mvvm.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MvvmBaseTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCM001";
        private const string Category = "Scaffold.MVVM";
        private const string ViewModelInterface = "Scaffold.MVVM.IViewModel";
        private const string InpcInterface = "System.ComponentModel.INotifyPropertyChanged";
        private const string ViewModelBaseType = "Scaffold.MVVM.ViewModel";
        private const string ModelBaseType = "Scaffold.MVVM.Model";
        private const string CoreMvvmPathSegment = "/Assets/Scripts/Core/MVVM/";
        private const string InfraMvvmPathSegment = "/Assets/Scripts/Infra/MVVM/";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "MVVM classes should use module base types",
            "Error SCM001: Class '{0}' uses manual MVVM interfaces. Inherit from '{1}' and declare the class as partial to align with MVVM source generation conventions.",
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
            if (context.Symbol is not INamedTypeSymbol typeSymbol) return;

            var sourceLoc = typeSymbol.Locations.FirstOrDefault(l => l.SourceTree != null);
            if (sourceLoc?.SourceTree == null) return;
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(sourceLoc.SourceTree);
            if (!Scaffold.Analyzers.AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            if (!IsCandidateClass(typeSymbol)) return;

            var location = typeSymbol.Locations.FirstOrDefault(locationItem => locationItem.IsInSource);
            if (location == null) return;

            bool implementsIViewModel = ImplementsInterface(typeSymbol, ViewModelInterface);
            bool inheritsViewModelBase = InheritsFrom(typeSymbol, ViewModelBaseType);
            bool directlyInheritsObject = InheritsDirectlyFromObject(typeSymbol);
            if (implementsIViewModel && directlyInheritsObject && !inheritsViewModelBase)
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
            if (typeSymbol.TypeKind != TypeKind.Class) return false;
            if (typeSymbol.Name == "ViewModel" || typeSymbol.Name == "Model") return false;

            var sourceLocation = typeSymbol.Locations.FirstOrDefault(locationItem => locationItem.IsInSource);
            if (sourceLocation == null) return false;

            var filePath = sourceLocation.SourceTree?.FilePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            var normalizedPath = filePath.Replace('\\', '/');
            bool isMvvmFile =
                normalizedPath.IndexOf(CoreMvvmPathSegment, StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalizedPath.IndexOf(InfraMvvmPathSegment, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isMvvmFile) return false;

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

        private static bool InheritsDirectlyFromObject(INamedTypeSymbol symbol)
        {
            if (symbol.BaseType == null) return true;
            return symbol.BaseType.SpecialType == SpecialType.System_Object;
        }
    }
}

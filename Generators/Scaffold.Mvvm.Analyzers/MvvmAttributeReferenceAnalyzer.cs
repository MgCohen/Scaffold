using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Mvvm.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MvvmAttributeReferenceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCM002";
        private const string Category = "Scaffold.MVVM";

        private static readonly ImmutableHashSet<string> KnownAttributes = ImmutableHashSet.Create(
            "ObservableProperty",
            "ObservablePropertyAttribute",
            "NestedObservableObject",
            "NestedObservableObjectAttribute",
            "BindSource",
            "BindSourceAttribute");

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "MVVM generator attributes require references",
            "Error SCM002: Attribute '{0}' is unresolved. Add MVVM references for CommunityToolkit.Mvvm and source generators (CommunityToolkit.Mvvm.SourceGenerators and MVVMCompositionGenerator) to the owning assembly.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "MVVM generator attributes must resolve to known types. Missing references break source generation and runtime binding contracts.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        }

        private void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            if (Scaffold.Analyzers.ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!Scaffold.Analyzers.AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var attributeSyntax = (AttributeSyntax)context.Node;
            string attributeName = GetAttributeName(attributeSyntax);
            if (!KnownAttributes.Contains(attributeName)) return;

            var typeInfo = context.SemanticModel.GetTypeInfo(attributeSyntax);
            bool unresolved = typeInfo.Type == null || typeInfo.Type.Kind == SymbolKind.ErrorType;
            if (!unresolved) return;

            var diagnostic = Diagnostic.Create(rule, attributeSyntax.Name.GetLocation(), attributeName);
            context.ReportDiagnostic(diagnostic);
        }

        private static string GetAttributeName(AttributeSyntax attributeSyntax)
        {
            return attributeSyntax.Name switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
                AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.Text,
                _ => attributeSyntax.Name.ToString().Split('.').Last(),
            };
        }
    }
}

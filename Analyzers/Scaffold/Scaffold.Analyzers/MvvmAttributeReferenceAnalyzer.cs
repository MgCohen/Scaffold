using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MvvmAttributeReferenceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0016";
        private const string Category = "Dependency";

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
            "Error SCA0016: Attribute '{0}' is unresolved. Add MVVM references for CommunityToolkit.Mvvm and source generators (CommunityToolkit.Mvvm.SourceGenerators and ObservableNestedPropertiesGenerator) to the owning assembly.",
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
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

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

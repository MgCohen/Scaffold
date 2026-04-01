using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SingleTopLevelNamespaceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA3004";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                NamespaceLayoutDescriptors.MultipleTopLevelNamespacesRule,
                NamespaceLayoutDescriptors.TypeOutsideTopLevelNamespaceRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxTreeAction(ctx =>
                NamespaceLayoutAnalysis.AnalyzeSyntaxTree(
                    ctx,
                    NamespaceLayoutRuleKind.Sca3004MultipleNamespaces | NamespaceLayoutRuleKind.Sca3004TypeOutsideBlock));
        }
    }
}

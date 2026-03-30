using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NamespaceRootAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA3005";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(NamespaceLayoutDescriptors.NamespaceRootRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxTreeAction(ctx =>
                NamespaceLayoutAnalysis.AnalyzeSyntaxTree(ctx, NamespaceLayoutRuleKind.Sca3005));
        }
    }
}

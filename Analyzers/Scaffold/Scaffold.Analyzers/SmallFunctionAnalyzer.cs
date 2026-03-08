using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SmallFunctionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0006";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Methods should be small and focused";
        private static readonly LocalizableString MessageFormat = "Error SCA0006: Method '{0}' has {1} lines of code, exceeding the {2}-line limit. Refactor and extract procedural parts into smaller, well-named private methods.";
        private static readonly LocalizableString Description = "Keep methods under 8 lines of code. Refactor by extracting steps into well-named methods.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);
            var maxLines = AnalyzerConfig.GetInt(options, "scaffold.SCA0006.max_lines", 8);

            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            if (methodDeclaration.Body == null)
            {
                return;
            }

            // Count lines inside the body
            var lineSpan = methodDeclaration.Body.SyntaxTree.GetLineSpan(methodDeclaration.Body.Span);
            var startLine = lineSpan.StartLinePosition.Line;
            var endLine = lineSpan.EndLinePosition.Line;

            // Subtract 1 because the braces themselves don't count towards the logic length in typical metrics,
            // but even if we do simple counting, > 10 lines total might be a good heuristic for "8 lines of logic".
            // Let's count statements instead, or just direct line numbers inside the block.
            var lineCount = (endLine - startLine) - 1;

            if (lineCount > maxLines)
            {
                var diagnostic = Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), methodDeclaration.Identifier.Text, lineCount, maxLines);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

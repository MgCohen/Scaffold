using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MethodCommentAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0001";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "No comments on methods allowed";
        private static readonly LocalizableString MessageFormat = "Error SCA0001: Method '{0}' has an invalid comment. Remove the comment entirely or change it to include 'todo' or 'sample'.";
        private static readonly LocalizableString Description = "Methods must not have XML or inline comments attached. The only exceptions are 'todo' and 'sample'.";

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

            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Check leading trivia
            if (methodDeclaration.HasLeadingTrivia)
            {
                foreach (var trivia in methodDeclaration.GetLeadingTrivia())
                {
                    if (IsComment(trivia) && !IsAllowedComment(trivia))
                    {
                        var diagnostic = Diagnostic.Create(rule, trivia.GetLocation(), methodDeclaration.Identifier.Text);
                        context.ReportDiagnostic(diagnostic);
                        break;
                    }
                }
            }
        }

        private static bool IsComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                   trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                   trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                   trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        }

        private static bool IsAllowedComment(SyntaxTrivia trivia)
        {
            // allowed: 'todo' and 'sample'
            var text = trivia.ToFullString().ToLowerInvariant();
            return text.Contains("todo") || text.Contains("sample");
        }
    }
}

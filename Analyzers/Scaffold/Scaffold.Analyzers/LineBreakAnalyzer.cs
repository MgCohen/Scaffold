using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LineBreakAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0005";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Avoid line breaks in signatures and method bodies";
        private static readonly LocalizableString MessageFormat = "Error SCA0005: Line breaks found inside a single statement/signature. Collapse the statement back onto a single line without newlines.";
        private static readonly LocalizableString Description = "Method signatures remain on one line. Inside methods, keep each statement or expression on one line.";

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

            context.RegisterSyntaxNodeAction(AnalyzeMethodSignature, SyntaxKind.MethodDeclaration);
            // Basic statement blocks
            context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
            context.RegisterSyntaxNodeAction(AnalyzeLocalDeclarationStatement, SyntaxKind.LocalDeclarationStatement);
        }

        private void AnalyzeMethodSignature(SyntaxNodeAnalysisContext context)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Basic check: does the parameter list or modifiers span multiple lines?
            // To do this reliably, we can check the line span of the block from return type to end of parameter list.
            var startLine = methodDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line;
            var paramEndLine = methodDeclaration.ParameterList.GetLocation().GetLineSpan().EndLinePosition.Line;

            if (startLine != paramEndLine && methodDeclaration.ParameterList.Parameters.Count > 0)
            {
                var diagnostic = Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            var statement = (ExpressionStatementSyntax)context.Node;

            // Allow builder/fluent patterns which typically look like multiline chained method calls on new lines
            // We'll skip enforcing multi-line purely on InvocationExpression with member access to prevent over-flagging fluent patterns.
            if (statement.Expression is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax)
            {
                return;
            }

            var lineSpan = statement.GetLocation().GetLineSpan();
            if (lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line)
            {
                var diagnostic = Diagnostic.Create(rule, statement.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeLocalDeclarationStatement(SyntaxNodeAnalysisContext context)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            var statement = (LocalDeclarationStatementSyntax)context.Node;

            var lineSpan = statement.GetLocation().GetLineSpan();
            if (lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line)
            {
                var diagnostic = Diagnostic.Create(rule, statement.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

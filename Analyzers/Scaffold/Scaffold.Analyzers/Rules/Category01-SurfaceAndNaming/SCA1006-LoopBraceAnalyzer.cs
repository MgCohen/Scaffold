using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class LoopBraceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA1006";
        private const string Category = "Style";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Loop bodies must use braces on the next line",
            "Error SCA1006: {0} must use braces and place the body on the next line.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "for/foreach/while/do-while statements must use braces and the opening brace must be on the next line.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeForStatement, SyntaxKind.ForStatement);
            context.RegisterSyntaxNodeAction(AnalyzeForEachStatement, SyntaxKind.ForEachStatement);
            context.RegisterSyntaxNodeAction(AnalyzeWhileStatement, SyntaxKind.WhileStatement);
            context.RegisterSyntaxNodeAction(AnalyzeDoStatement, SyntaxKind.DoStatement);
        }

        private static void AnalyzeForStatement(SyntaxNodeAnalysisContext context)
        {
            var statement = (ForStatementSyntax)context.Node;
            AnalyzeLoop(context, statement.Statement, statement.CloseParenToken, "for statement");
        }

        private static void AnalyzeForEachStatement(SyntaxNodeAnalysisContext context)
        {
            var statement = (ForEachStatementSyntax)context.Node;
            AnalyzeLoop(context, statement.Statement, statement.CloseParenToken, "foreach statement");
        }

        private static void AnalyzeWhileStatement(SyntaxNodeAnalysisContext context)
        {
            var statement = (WhileStatementSyntax)context.Node;
            AnalyzeLoop(context, statement.Statement, statement.CloseParenToken, "while statement");
        }

        private static void AnalyzeDoStatement(SyntaxNodeAnalysisContext context)
        {
            var statement = (DoStatementSyntax)context.Node;
            AnalyzeLoop(context, statement.Statement, statement.DoKeyword, "do-while statement");
        }

        private static void AnalyzeLoop(
            SyntaxNodeAnalysisContext context,
            StatementSyntax loopBody,
            SyntaxToken headerToken,
            string loopLabel)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath))
            {
                return;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule))
            {
                return;
            }

            if (!(loopBody is BlockSyntax block))
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, loopBody.GetLocation(), loopLabel));
                return;
            }

            var headerLine = headerToken.GetLocation().GetLineSpan().EndLinePosition.Line;
            var openBraceLine = block.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (headerLine == openBraceLine)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, block.OpenBraceToken.GetLocation(), loopLabel));
            }
        }
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ReadableLoopAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0014";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Loops should not be compressed into a single line";
        private static readonly LocalizableString MessageFormat = "Error SCA0014: Loop inside method '{0}' is compressed into one line. Expand header and body onto separate lines.";
        private static readonly LocalizableString Description = "Compressed single-line loops reduce readability. Use a multi-line loop body. Compact single-line guard clauses are allowed.";

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
            context.RegisterSyntaxNodeAction(AnalyzeForLoop, SyntaxKind.ForStatement);
            context.RegisterSyntaxNodeAction(AnalyzeForEachLoop, SyntaxKind.ForEachStatement);
        }

        private void AnalyzeForLoop(SyntaxNodeAnalysisContext context)
        {
            AnalyzeLoop(context, (StatementSyntax)context.Node);
        }

        private void AnalyzeForEachLoop(SyntaxNodeAnalysisContext context)
        {
            AnalyzeLoop(context, (StatementSyntax)context.Node);
        }

        private void AnalyzeLoop(SyntaxNodeAnalysisContext context, StatementSyntax loopStatement)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) { return; }
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);
            bool isTargetPath = IsTargetPath(context.Node.SyntaxTree.FilePath);
            if (!isTargetPath) { return; }

            bool isSingleLineLoop = IsSingleLine(loopStatement);
            if (!isSingleLineLoop) { return; }
            bool isReadableCompactLoop = IsReadableCompactLoop(loopStatement);
            if (isReadableCompactLoop) { return; }
            bool isAllowedGuardClause = IsAllowedCompactGuardClause(loopStatement);
            if (isAllowedGuardClause) { return; }

            string methodName = GetContainingMethodName(loopStatement);
            var diagnostic = Diagnostic.Create(rule, loopStatement.GetLocation(), methodName);
            context.ReportDiagnostic(diagnostic);
        }

        private bool IsTargetPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) { return false; }
            string normalized = filePath.Replace('/', '\\');
            return normalized.Contains("Assets\\Scripts\\Core\\Entities\\Runtime\\");
        }

        private bool IsSingleLine(StatementSyntax loopStatement)
        {
            var lineSpan = loopStatement.GetLocation().GetLineSpan();
            return lineSpan.StartLinePosition.Line == lineSpan.EndLinePosition.Line;
        }

        private bool IsAllowedCompactGuardClause(StatementSyntax loopStatement)
        {
            StatementSyntax body = GetLoopBody(loopStatement);
            if (body == null) { return false; }
            if (body is not BlockSyntax block) { return false; }
            if (block.Statements.Count != 1) { return false; }
            if (block.Statements[0] is not IfStatementSyntax ifStatement) { return false; }
            return IsCompactGuardClause(ifStatement);
        }

        private bool IsReadableCompactLoop(StatementSyntax loopStatement)
        {
            StatementSyntax body = GetLoopBody(loopStatement);
            if (body is not BlockSyntax block) { return true; }
            if (block.Statements.Count == 0) { return true; }
            if (block.Statements.Count == 1 && block.Statements[0] is not IfStatementSyntax) { return true; }
            return false;
        }

        private StatementSyntax GetLoopBody(StatementSyntax loopStatement)
        {
            if (loopStatement is ForStatementSyntax forStatement) { return forStatement.Statement; }
            if (loopStatement is ForEachStatementSyntax forEachStatement) { return forEachStatement.Statement; }
            return null;
        }

        private bool IsCompactGuardClause(IfStatementSyntax ifStatement)
        {
            if (ifStatement.Else != null) { return false; }
            if (!IsSingleLine(ifStatement)) { return false; }
            if (ifStatement.Statement is not BlockSyntax block) { return false; }
            if (block.Statements.Count != 2) { return false; }
            bool hasAssignment = IsAssignmentStatement(block.Statements[0]);
            bool hasReturn = block.Statements[1] is ReturnStatementSyntax;
            return hasAssignment && hasReturn;
        }

        private bool IsAssignmentStatement(StatementSyntax statement)
        {
            if (statement is not ExpressionStatementSyntax expressionStatement) { return false; }
            return expressionStatement.Expression is AssignmentExpressionSyntax;
        }

        private string GetContainingMethodName(SyntaxNode syntaxNode)
        {
            MethodDeclarationSyntax method = syntaxNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (method == null) { return "<unknown>"; }
            return method.Identifier.Text;
        }
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConditionalBraceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA1007";
        private const string Category = "Style";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Conditionals must use braces except approved inline single-statement if",
            "Error SCA1007: {0} must use braces with body on the next line. Only single-line `if (...) statement;` without `else` may omit braces.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "if/else branches must use braces and multiline formatting, except an inline single-statement if without else.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
        }

        private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
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
            var statement = (IfStatementSyntax)context.Node;

            if (IsAllowedInlineIf(statement))
            {
                return;
            }

            if (!HasBracesOnNextLine(statement.Statement, statement.CloseParenToken))
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, statement.IfKeyword.GetLocation(), "if branch"));
                return;
            }

            if (statement.Else == null)
            {
                return;
            }

            if (!HasBracesOnNextLine(statement.Else.Statement, statement.Else.ElseKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, statement.Else.ElseKeyword.GetLocation(), "else branch"));
            }
        }

        private static bool IsAllowedInlineIf(IfStatementSyntax statement)
        {
            if (statement.Else != null)
            {
                return false;
            }

            if (statement.Statement is BlockSyntax)
            {
                return false;
            }

            var conditionLine = statement.CloseParenToken.GetLocation().GetLineSpan().EndLinePosition.Line;
            var statementLineSpan = statement.Statement.GetLocation().GetLineSpan();
            return conditionLine == statementLineSpan.StartLinePosition.Line &&
                   statementLineSpan.StartLinePosition.Line == statementLineSpan.EndLinePosition.Line;
        }

        private static bool HasBracesOnNextLine(StatementSyntax branchStatement, SyntaxToken headerToken)
        {
            if (!(branchStatement is BlockSyntax block))
            {
                return false;
            }

            var headerLine = headerToken.GetLocation().GetLineSpan().EndLinePosition.Line;
            var openBraceLine = block.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line;
            return openBraceLine > headerLine;
        }
    }
}

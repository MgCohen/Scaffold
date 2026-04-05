using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LineBreakAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA1003";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Line break and switch formatting";
        private static readonly LocalizableString MessageFormat = "Error SCA1003: {0}";
        private static readonly LocalizableString Description =
            "Style rules for signatures, type attributes, statements, and switch expressions.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        private const string MsgSignatureCollapse =
            "Line breaks found inside a single statement/signature. Collapse the statement back onto a single line without newlines.";

        private const string MsgTypeAttributesSeparate =
            "Place type attributes on separate lines above the declaration; do not place attributes on the same line as the class, interface, struct, or record keyword.";

        private const string MsgSwitchMultiline =
            "Format switch expressions and switch statements across multiple lines: put the opening brace on the line after `switch`, and put each case/arm on its own line.";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMethodSignature, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeConstructorSignature, SyntaxKind.ConstructorDeclaration);
            context.RegisterSyntaxNodeAction(
                AnalyzeTypeDeclarationSignature,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.RecordDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
            context.RegisterSyntaxNodeAction(AnalyzeLocalDeclarationStatement, SyntaxKind.LocalDeclarationStatement);
            context.RegisterSyntaxNodeAction(AnalyzeSwitchExpression, SyntaxKind.SwitchExpression);
            context.RegisterSyntaxNodeAction(AnalyzeSwitchStatement, SyntaxKind.SwitchStatement);
        }

        private void AnalyzeMethodSignature(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            var startLine = GetLineOfFirstTokenAfterAttributeLists(methodDeclaration);
            var signatureEndLine = GetMethodSignatureEndLine(methodDeclaration);

            if (startLine != signatureEndLine &&
                (methodDeclaration.ParameterList.Parameters.Count > 0 || methodDeclaration.ConstraintClauses.Count > 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), MsgSignatureCollapse));
            }
        }

        private void AnalyzeTypeDeclarationSignature(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var typeDeclaration = (TypeDeclarationSyntax)context.Node;

            if (typeDeclaration.AttributeLists.Count > 0)
            {
                var lastAttrEndLine = typeDeclaration.AttributeLists.Last().GetLocation().GetLineSpan().EndLinePosition.Line;
                var keywordLine = typeDeclaration.Keyword.GetLocation().GetLineSpan().StartLinePosition.Line;
                if (keywordLine <= lastAttrEndLine)
                {
                    context.ReportDiagnostic(Diagnostic.Create(rule, typeDeclaration.Keyword.GetLocation(), MsgTypeAttributesSeparate));
                }
            }

            var typeParameterList = typeDeclaration.TypeParameterList;
            var hasTypeParameters = typeParameterList != null && typeParameterList.Parameters.Count > 0;
            var hasConstraints = typeDeclaration.ConstraintClauses.Count > 0;
            var hasPrimaryCtorParams = typeDeclaration is RecordDeclarationSyntax recordDecl &&
                recordDecl.ParameterList != null &&
                recordDecl.ParameterList.Parameters.Count > 0;

            if (!hasTypeParameters && !hasConstraints && !hasPrimaryCtorParams)
            {
                return;
            }

            var signatureStartLine = typeDeclaration.Keyword.GetLocation().GetLineSpan().StartLinePosition.Line;
            var signatureEndLine = GetTypeSignatureEndLine(typeDeclaration);

            if (signatureStartLine != signatureEndLine)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, typeDeclaration.Identifier.GetLocation(), MsgSignatureCollapse));
            }
        }

        private void AnalyzeConstructorSignature(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var constructorDeclaration = (ConstructorDeclarationSyntax)context.Node;
            var startLine = GetLineOfFirstTokenAfterAttributeLists(constructorDeclaration);
            var paramEndLine = constructorDeclaration.ParameterList.GetLocation().GetLineSpan().EndLinePosition.Line;

            if (startLine != paramEndLine && constructorDeclaration.ParameterList.Parameters.Count > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, constructorDeclaration.Identifier.GetLocation(), MsgSignatureCollapse));
                return;
            }

            if (constructorDeclaration.Initializer == null)
            {
                return;
            }

            var initSpan = constructorDeclaration.Initializer.GetLocation().GetLineSpan();
            if (initSpan.StartLinePosition.Line != paramEndLine ||
                initSpan.StartLinePosition.Line != initSpan.EndLinePosition.Line)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, constructorDeclaration.Identifier.GetLocation(), MsgSignatureCollapse));
            }
        }

        private void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var statement = (ExpressionStatementSyntax)context.Node;
            if (ContainsInitializer(statement)) return;

            if (statement.DescendantNodes().OfType<SwitchExpressionSyntax>().Any())
            {
                return;
            }

            if (statement.Expression is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax)
            {
                return;
            }

            var lineSpan = statement.GetLocation().GetLineSpan();
            if (lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, statement.GetLocation(), MsgSignatureCollapse));
            }
        }

        private void AnalyzeLocalDeclarationStatement(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var statement = (LocalDeclarationStatementSyntax)context.Node;
            if (ContainsInitializer(statement)) return;

            if (statement.DescendantNodes().OfType<SwitchExpressionSyntax>().Any())
            {
                return;
            }

            var lineSpan = statement.GetLocation().GetLineSpan();
            if (lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, statement.GetLocation(), MsgSignatureCollapse));
            }
        }

        private void AnalyzeSwitchExpression(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var switchExpr = (SwitchExpressionSyntax)context.Node;

            var switchLine = switchExpr.SwitchKeyword.GetLocation().GetLineSpan().StartLinePosition.Line;
            var openBraceLine = switchExpr.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (switchLine >= openBraceLine)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, switchExpr.SwitchKeyword.GetLocation(), MsgSwitchMultiline));
                return;
            }

            var arms = switchExpr.Arms;
            if (arms.Count < 2)
            {
                return;
            }

            var lineByArm = new List<int>(arms.Count);
            foreach (var arm in arms)
            {
                lineByArm.Add(arm.GetLocation().GetLineSpan().StartLinePosition.Line);
            }

            for (var i = 1; i < lineByArm.Count; i++)
            {
                if (lineByArm[i] == lineByArm[i - 1])
                {
                    context.ReportDiagnostic(Diagnostic.Create(rule, arms[i].GetLocation(), MsgSwitchMultiline));
                    return;
                }
            }
        }

        private void AnalyzeSwitchStatement(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var switchStmt = (SwitchStatementSyntax)context.Node;

            var switchLine = switchStmt.SwitchKeyword.GetLocation().GetLineSpan().StartLinePosition.Line;
            var openBraceLine = switchStmt.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (switchLine >= openBraceLine)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, switchStmt.SwitchKeyword.GetLocation(), MsgSwitchMultiline));
            }
        }

        private static bool ContainsInitializer(StatementSyntax statement)
        {
            return statement.DescendantNodes().OfType<InitializerExpressionSyntax>().Any();
        }

        private static int GetLineOfFirstTokenAfterAttributeLists(MemberDeclarationSyntax member)
        {
            if (member.AttributeLists.Count == 0)
            {
                return member.GetLocation().GetLineSpan().StartLinePosition.Line;
            }

            var tokenAfter = member.AttributeLists.Last().GetLastToken().GetNextToken();
            return tokenAfter.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        private static int GetMethodSignatureEndLine(MethodDeclarationSyntax methodDeclaration)
        {
            if (methodDeclaration.ConstraintClauses.Count > 0)
            {
                var lastClause = methodDeclaration.ConstraintClauses[methodDeclaration.ConstraintClauses.Count - 1];
                return lastClause.GetLocation().GetLineSpan().EndLinePosition.Line;
            }

            return methodDeclaration.ParameterList.GetLocation().GetLineSpan().EndLinePosition.Line;
        }

        private static int GetTypeSignatureEndLine(TypeDeclarationSyntax typeDeclaration)
        {
            if (typeDeclaration.ConstraintClauses.Count > 0)
            {
                var lastClause = typeDeclaration.ConstraintClauses[typeDeclaration.ConstraintClauses.Count - 1];
                return lastClause.GetLocation().GetLineSpan().EndLinePosition.Line;
            }

            if (typeDeclaration is RecordDeclarationSyntax recordDeclaration &&
                recordDeclaration.ParameterList != null &&
                recordDeclaration.ParameterList.Parameters.Count > 0)
            {
                return recordDeclaration.ParameterList.GetLocation().GetLineSpan().EndLinePosition.Line;
            }

            var typeParameterList = typeDeclaration.TypeParameterList;
            if (typeParameterList != null)
            {
                return typeParameterList.GetLocation().GetLineSpan().EndLinePosition.Line;
            }

            return typeDeclaration.Identifier.GetLocation().GetLineSpan().EndLinePosition.Line;
        }
    }
}

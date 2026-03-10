using System;
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
    public class InvariantEntryPointAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0014";
        private const string Category = "Correctness";
        private const string AllowedPrefixesKey = "scaffold.SCA0014.allowed_prefixes";

        private static readonly string[] DefaultPrefixes = { "Validate", "TryValidate", "Ensure", "Guard" };

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Public runtime entry points must validate invariants on entry",
            "Error SCA0014: Public runtime method '{0}' does not validate invariants at entry. Add a leading guard clause (`if (...) return/throw`) or call a validation method (`Validate*`, `TryValidate*`, `Ensure*`, `Guard*`) before business logic.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Public runtime entry points should validate input and state invariants at method entry.");

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
            if (!IsCandidateEntryPoint(context, methodDeclaration)) return;

            var allowedPrefixes = GetAllowedPrefixes(options);
            if (HasEntryValidation(methodDeclaration, allowedPrefixes)) return;

            var diagnostic = Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), methodDeclaration.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsCandidateEntryPoint(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration)
        {
            if (methodDeclaration.Body == null) return false;

            var symbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            if (symbol == null) return false;
            if (symbol.DeclaredAccessibility != Accessibility.Public) return false;
            if (symbol.IsOverride) return false;
            if (symbol.MethodKind != MethodKind.Ordinary) return false;
            if (symbol.ContainingType?.TypeKind == TypeKind.Interface) return false;

            return IsRuntimePath(methodDeclaration.SyntaxTree.FilePath);
        }

        private static bool IsRuntimePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            var normalized = filePath.Replace('\\', '/');

            if (normalized.IndexOf("/tests/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (normalized.IndexOf("/samples/", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return normalized.IndexOf("/runtime/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ImmutableArray<string> GetAllowedPrefixes(AnalyzerConfigOptions options)
        {
            var prefixes = new HashSet<string>(DefaultPrefixes, StringComparer.OrdinalIgnoreCase);

            if (options.TryGetValue(AllowedPrefixesKey, out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                var extra = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in extra)
                {
                    var prefix = item.Trim();
                    if (prefix.Length == 0) continue;
                    prefixes.Add(prefix);
                }
            }

            return prefixes.ToImmutableArray();
        }

        private static bool HasEntryValidation(MethodDeclarationSyntax methodDeclaration, ImmutableArray<string> allowedPrefixes)
        {
            var statements = methodDeclaration.Body.Statements;
            if (statements.Count == 0) return false;

            var index = 0;
            while (index < statements.Count && statements[index] is LocalDeclarationStatementSyntax)
            {
                index++;
            }

            if (index >= statements.Count) return false;

            var firstExecutable = statements[index];
            if (IsGuardClause(firstExecutable)) return true;
            if (IsValidationCall(firstExecutable, allowedPrefixes)) return true;

            return false;
        }

        private static bool IsGuardClause(StatementSyntax statement)
        {
            if (!(statement is IfStatementSyntax ifStatement)) return false;
            if (ifStatement.Else != null) return false;

            return IsExitStatement(ifStatement.Statement);
        }

        private static bool IsExitStatement(StatementSyntax statement)
        {
            if (statement is ReturnStatementSyntax) return true;
            if (statement is ThrowStatementSyntax) return true;

            if (statement is BlockSyntax block && block.Statements.Count == 1)
            {
                return IsExitStatement(block.Statements[0]);
            }

            return false;
        }

        private static bool IsValidationCall(StatementSyntax statement, ImmutableArray<string> allowedPrefixes)
        {
            if (!TryGetInvocationName(statement, out var invocationName)) return false;
            return allowedPrefixes.Any(prefix => invocationName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetInvocationName(StatementSyntax statement, out string invocationName)
        {
            invocationName = null;

            if (!(statement is ExpressionStatementSyntax expressionStatement)) return false;

            if (expressionStatement.Expression is InvocationExpressionSyntax invocation)
            {
                return TryGetInvokedIdentifier(invocation.Expression, out invocationName);
            }

            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment &&
                assignment.Right is InvocationExpressionSyntax assignedInvocation)
            {
                return TryGetInvokedIdentifier(assignedInvocation.Expression, out invocationName);
            }

            return false;
        }

        private static bool TryGetInvokedIdentifier(ExpressionSyntax expression, out string invocationName)
        {
            invocationName = null;

            if (expression is IdentifierNameSyntax identifier)
            {
                invocationName = identifier.Identifier.Text;
                return true;
            }

            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                invocationName = memberAccess.Name.Identifier.Text;
                return true;
            }

            if (expression is MemberBindingExpressionSyntax memberBinding)
            {
                invocationName = memberBinding.Name.Identifier.Text;
                return true;
            }

            return false;
        }
    }
}

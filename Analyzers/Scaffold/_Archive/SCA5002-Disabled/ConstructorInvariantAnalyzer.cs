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
    public sealed class ConstructorInvariantAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA5002";
        private const string Category = "Correctness";
        private const string AllowedPrefixesKey = "scaffold.SCA5002.allowed_prefixes";
        private const string PrimitiveSemanticTokensKey = "scaffold.SCA5002.primitive_semantic_tokens";

        private static readonly string[] DefaultPrefixes = { "Validate", "TryValidate", "Ensure", "Guard" };
        private static readonly string[] DefaultPrimitiveSemanticTokens =
        {
            "index", "count", "length", "size", "capacity", "offset", "position",
            "min", "max", "minimum", "maximum", "range"
        };

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Public runtime constructors must validate invariants on entry",
            "Error SCA5002: Public runtime constructor '{0}' does not validate constructor parameters at entry. Add a leading guard clause (`if (...) throw ...`) or call a validation method (`Validate*`, `TryValidate*`, `Ensure*`, `Guard*`) before assignments/business logic.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Public runtime constructors with parameters should validate input/state invariants at constructor entry.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        }

        private void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            var constructorDeclaration = (ConstructorDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(constructorDeclaration);
            if (!IsCandidateEntryPoint(constructorDeclaration, symbol)) return;
            if (symbol == null) return;
            if (!InvariantUsageScope.ShouldValidateForType(context.Compilation, symbol.ContainingType)) return;
            if (ShouldSkipForPrimitiveLikeParameters(symbol, options)) return;

            var allowedPrefixes = GetAllowedPrefixes(options);
            if (HasEntryValidation(context, constructorDeclaration, allowedPrefixes)) return;

            var diagnostic = Diagnostic.Create(rule, constructorDeclaration.Identifier.GetLocation(), constructorDeclaration.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsCandidateEntryPoint(
            ConstructorDeclarationSyntax constructorDeclaration,
            IMethodSymbol symbol)
        {
            if (constructorDeclaration.Body == null) return false;
            if (constructorDeclaration.Initializer != null) return false;

            if (symbol == null) return false;
            if (symbol.DeclaredAccessibility != Accessibility.Public) return false;
            if (symbol.MethodKind != MethodKind.Constructor) return false;
            if (symbol.Parameters.Length == 0) return false;

            return IsRuntimePath(constructorDeclaration.SyntaxTree.FilePath);
        }

        private static bool IsRuntimePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            var normalized = filePath.Replace('\\', '/');
            var analysisPath = normalized;
            var assetsIndex = normalized.IndexOf("/assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                analysisPath = normalized.Substring(assetsIndex);
            }

            if (analysisPath.IndexOf("/tests/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (analysisPath.IndexOf("/samples/", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return analysisPath.IndexOf("/runtime/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldSkipForPrimitiveLikeParameters(IMethodSymbol constructorSymbol, AnalyzerConfigOptions options)
        {
            if (constructorSymbol == null) return false;
            if (constructorSymbol.Parameters.Length == 0) return false;
            if (constructorSymbol.Parameters.Any(parameter => !IsPrimitiveLike(parameter.Type))) return false;

            var semanticTokens = GetPrimitiveSemanticTokens(options);
            return !constructorSymbol.Parameters.Any(parameter => HasSemanticParameterName(parameter.Name, semanticTokens));
        }

        private static ImmutableArray<string> GetPrimitiveSemanticTokens(AnalyzerConfigOptions options)
        {
            var tokens = new HashSet<string>(DefaultPrimitiveSemanticTokens, StringComparer.OrdinalIgnoreCase);

            if (options.TryGetValue(PrimitiveSemanticTokensKey, out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                var extra = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in extra)
                {
                    var token = item.Trim();
                    if (token.Length == 0) continue;
                    tokens.Add(token);
                }
            }

            return tokens.ToImmutableArray();
        }

        private static bool HasSemanticParameterName(string parameterName, ImmutableArray<string> semanticTokens)
        {
            if (string.IsNullOrWhiteSpace(parameterName)) return false;
            return semanticTokens.Any(token => parameterName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsPrimitiveLike(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return false;
            if (typeSymbol.TypeKind == TypeKind.Enum) return true;

            if (typeSymbol is INamedTypeSymbol namedType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                namedType.TypeArguments.Length == 1)
            {
                return IsPrimitiveLike(namedType.TypeArguments[0]);
            }

            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Char:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_String:
                    return true;
                default:
                    return false;
            }
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

        private static bool HasEntryValidation(
            SyntaxNodeAnalysisContext context,
            ConstructorDeclarationSyntax constructorDeclaration,
            ImmutableArray<string> allowedPrefixes)
        {
            var statements = constructorDeclaration.Body.Statements;
            if (statements.Count == 0) return false;

            var index = 0;
            while (index < statements.Count && statements[index] is LocalDeclarationStatementSyntax)
            {
                index++;
            }

            if (index >= statements.Count) return false;

            var firstExecutable = statements[index];
            if (IsGuardClause(firstExecutable)) return true;
            if (IsNullCoalescingParameterGuard(firstExecutable, constructorDeclaration)) return true;
            if (IsArgumentNullExceptionThrowIfNull(context, firstExecutable)) return true;
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

        private static bool IsNullCoalescingParameterGuard(
            StatementSyntax statement,
            ConstructorDeclarationSyntax constructorDeclaration)
        {
            if (!(statement is ExpressionStatementSyntax expressionStatement)) return false;
            if (!(expressionStatement.Expression is AssignmentExpressionSyntax assignment)) return false;
            if (!assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression)) return false;
            if (!(assignment.Left is IdentifierNameSyntax identifier)) return false;

            var parameterName = identifier.Identifier.Text;
            return constructorDeclaration.ParameterList.Parameters.Any(parameter =>
                string.Equals(parameter.Identifier.Text, parameterName, StringComparison.Ordinal));
        }

        private static bool IsArgumentNullExceptionThrowIfNull(
            SyntaxNodeAnalysisContext context,
            StatementSyntax statement)
        {
            if (!(statement is ExpressionStatementSyntax expressionStatement)) return false;
            if (!(expressionStatement.Expression is InvocationExpressionSyntax invocation)) return false;

            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
            if (methodSymbol == null) return false;
            if (!string.Equals(methodSymbol.Name, "ThrowIfNull", StringComparison.Ordinal)) return false;

            var containingType = methodSymbol.ContainingType?.ToDisplayString();
            return string.Equals(containingType, "System.ArgumentNullException", StringComparison.Ordinal);
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

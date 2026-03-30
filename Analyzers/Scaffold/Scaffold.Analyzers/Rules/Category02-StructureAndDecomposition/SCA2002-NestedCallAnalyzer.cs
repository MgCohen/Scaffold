using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NestedCallAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA2002";

        /// <summary>
        /// Editorconfig key. Value N means: report when computed nesting depth is &gt;= N.
        /// 1 = no call/object-creation in arguments (strict). 2 = allow one level (e.g. <c>Method(new Type())</c>).
        /// </summary>
        public const string MaxNestingDepthConfigKey = "scaffold.SCA2002.max_nesting_depth";

        private const string Category = "Style";

        private static readonly LocalizableString Title = "Do not nest function calls or object constructions";
        private static readonly LocalizableString MessageFormat = "Error SCA2002: Nested {0} found. Extract the nested call or object creation to a separate variable on the line above, and pass the variable as the argument instead.";
        private static readonly LocalizableString Description = "Avoid nesting function calls or object construction beyond the configured depth (see scaffold.SCA2002.max_nesting_depth). Assign results to intermediate variables.";

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

            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            AnalyzeArguments(context, invocation.ArgumentList);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var creation = (BaseObjectCreationExpressionSyntax)context.Node;
            if (creation.ArgumentList != null)
            {
                AnalyzeArguments(context, creation.ArgumentList);
            }
        }

        private void AnalyzeArguments(SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;
            var maxNestingDepth = AnalyzerConfig.GetInt(options, MaxNestingDepthConfigKey, 1);

            foreach (var argument in argumentList.Arguments)
            {
                var expression = argument.Expression;
                var depth = GetNestedCallDepth(expression);
                if (depth < maxNestingDepth)
                {
                    continue;
                }

                var stripped = expression;
                while (stripped is ParenthesizedExpressionSyntax paren)
                {
                    stripped = paren.Expression;
                }

                var kindName = stripped switch
                {
                    InvocationExpressionSyntax inv when !(inv.Expression is IdentifierNameSyntax id && id.Identifier.Text == "nameof") => "function call",
                    BaseObjectCreationExpressionSyntax => "object construction",
                    _ => "expression",
                };

                var diagnostic = Diagnostic.Create(rule, stripped.GetLocation(), kindName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        /// <summary>
        /// Depth of invocation/object-creation nesting along the deepest argument path (0 = no calls/creations).
        /// </summary>
        private static int GetNestedCallDepth(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax paren)
            {
                expression = paren.Expression;
            }

            if (expression is InvocationExpressionSyntax inv && inv.Expression is IdentifierNameSyntax id && id.Identifier.Text == "nameof")
            {
                return 0;
            }

            if (expression is InvocationExpressionSyntax invExpr)
            {
                var innerMax = 0;
                foreach (var arg in invExpr.ArgumentList.Arguments)
                {
                    var d = GetNestedCallDepth(arg.Expression);
                    if (d > innerMax) innerMax = d;
                }

                return 1 + innerMax;
            }

            if (expression is BaseObjectCreationExpressionSyntax baseCreate)
            {
                var innerMax = 0;
                if (baseCreate.ArgumentList != null)
                {
                    foreach (var arg in baseCreate.ArgumentList.Arguments)
                    {
                        var d = GetNestedCallDepth(arg.Expression);
                        if (d > innerMax) innerMax = d;
                    }
                }

                return 1 + innerMax;
            }

            return 0;
        }
    }
}

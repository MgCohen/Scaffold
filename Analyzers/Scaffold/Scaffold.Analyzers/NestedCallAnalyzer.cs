using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NestedCallAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0003";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Do not nest function calls or object constructions";
        private static readonly LocalizableString MessageFormat = "Error SCA0003: Nested {0} found. Extract the nested call or object creation to a separate variable on the line above, and pass the variable as the argument instead.";
        private static readonly LocalizableString Description = "Avoid nesting function calls or object construction more than one level deep. Assign results to intermediate variables.";

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
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            AnalyzeArguments(context, invocation.ArgumentList);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var creation = (ObjectCreationExpressionSyntax)context.Node;
            if (creation.ArgumentList != null)
            {
                AnalyzeArguments(context, creation.ArgumentList);
            }
        }

        private void AnalyzeArguments(SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            foreach (var argument in argumentList.Arguments)
            {
                var expression = argument.Expression;

                // Strip out parenthesis
                while (expression is ParenthesizedExpressionSyntax paren)
                {
                    expression = paren.Expression;
                }

                // If argument is an invocation or object creation...
                if (expression is InvocationExpressionSyntax || expression is ObjectCreationExpressionSyntax)
                {
                    // Exception: Nameof is allowed
                    if (expression is InvocationExpressionSyntax inv && inv.Expression is IdentifierNameSyntax id && id.Identifier.Text == "nameof")
                    {
                        continue;
                    }

                    // Report diagnostic
                    var kindName = expression is InvocationExpressionSyntax ? "function call" : "object construction";
                    var diagnostic = Diagnostic.Create(rule, expression.GetLocation(), kindName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}

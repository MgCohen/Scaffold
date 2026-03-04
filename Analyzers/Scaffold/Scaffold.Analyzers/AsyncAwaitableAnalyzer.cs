using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AsyncAwaitableAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0013";
        private const string Category = "Performance";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Give preference to Unity's Awaitable",
            "Error SCA0013: Method '{0}' returns '{1}'. Change the return type to Unity's Awaitable.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Use standard async/await (Task/ValueTask) only when Awaitable is not applicable.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            if (methodDeclaration.ReturnType is IdentifierNameSyntax identifierName)
            {
                var typeName = identifierName.Identifier.Text;
                if (typeName == "Task" || typeName == "ValueTask")
                {
                    var diagnostic = Diagnostic.Create(Rule, methodDeclaration.ReturnType.GetLocation(), methodDeclaration.Identifier.Text, typeName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
            else if (methodDeclaration.ReturnType is GenericNameSyntax genericName)
            {
                var typeName = genericName.Identifier.Text;
                if (typeName == "Task" || typeName == "ValueTask")
                {
                    var diagnostic = Diagnostic.Create(Rule, methodDeclaration.ReturnType.GetLocation(), methodDeclaration.Identifier.Text, typeName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}

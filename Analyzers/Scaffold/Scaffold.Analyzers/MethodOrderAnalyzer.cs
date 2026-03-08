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
    public class MethodOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0002";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Methods should be in order of usage";
        private static readonly LocalizableString MessageFormat = "Error SCA0002: Method '{0}' is called by '{1}'. Move the declaration of '{0}' so it appears sequentially *after* '{1}' in the file.";
        private static readonly LocalizableString Description = "Methods must be declared after the methods that use them. Internal classes stay at the end.";

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

            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeStruct, SyntaxKind.StructDeclaration);
        }

        private void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            AnalyzeType(context, (TypeDeclarationSyntax)context.Node);
        }

        private void AnalyzeStruct(SyntaxNodeAnalysisContext context)
        {
            AnalyzeType(context, (TypeDeclarationSyntax)context.Node);
        }

        private void AnalyzeType(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDeclaration)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            var methods = typeDeclaration.Members.OfType<MethodDeclarationSyntax>().ToList();
            var methodNames = new HashSet<string>(methods.Select(m => m.Identifier.Text));
            
            // Map method name to the earliest method that calls it
            var firstCallers = new Dictionary<string, MethodDeclarationSyntax>();

            foreach (var callerMethod in methods)
            {
                if (callerMethod.Body == null)
                    continue;

                // Find all simple calls by name inside the method body
                var invocations = callerMethod.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    string calledName = null;
                    if (invocation.Expression is IdentifierNameSyntax id)
                    {
                        calledName = id.Identifier.Text;
                    }
                    else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression is ThisExpressionSyntax)
                    {
                        calledName = memberAccess.Name.Identifier.Text;
                    }

                    if (calledName != null && methodNames.Contains(calledName))
                    {
                        if (!firstCallers.ContainsKey(calledName))
                        {
                            firstCallers[calledName] = callerMethod;
                        }
                    }
                }
            }

            // Verify order
            for (int i = 0; i < methods.Count; i++)
            {
                var method = methods[i];
                var name = method.Identifier.Text;

                if (firstCallers.TryGetValue(name, out var caller))
                {
                    var callerIndex = methods.IndexOf(caller);
                    if (callerIndex > i)
                    {
                        // The method appears before the method that calls it!
                        var diagnostic = Diagnostic.Create(rule, method.Identifier.GetLocation(), name, caller.Identifier.Text);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}

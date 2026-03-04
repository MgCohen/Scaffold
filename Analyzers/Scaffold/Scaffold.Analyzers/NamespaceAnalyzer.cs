using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NamespaceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA0009";
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Namespace must match folder structure",
            "Error SCA0009: Namespace '{0}' is structurally invalid. Rename the namespace to match the folder structure prefixed with the project name (e.g., 'ProjectName.Folder').",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Namespaces must be prefixed with the project name and match folder structure exactly. Special unity folders are omitted.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
        }

        private void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
        {
            var root = context.Tree.GetRoot(context.CancellationToken);
            var namespaceDecs = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().ToList();

            var filePath = context.Tree.FilePath;
            if (string.IsNullOrEmpty(filePath)) return;
            
            // Heuristic for finding root project
            // Since we can't easily get the base project name without options, we use the assembly name from options
            string expectedPrefix = "ProjectName"; // Need project name, but tree action doesn't have it easily.
            // Let's use AnalyzerOptions or just report the rule dynamically based on folder path segments.
            // For now, this is a placeholder implementation as dynamic checking needs more setup.
            
            if (namespaceDecs.Count == 0)
            {
                var diagnostic = Diagnostic.Create(Rule, root.GetLocation(), "<global>", "ProjectNamespace.Folder");
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

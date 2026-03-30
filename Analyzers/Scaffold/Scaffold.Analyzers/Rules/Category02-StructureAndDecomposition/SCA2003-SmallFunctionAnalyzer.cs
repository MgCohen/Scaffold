using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SmallFunctionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA2003";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Methods should be small and focused";
        private static readonly LocalizableString MessageFormat = "Error SCA2003: Method '{0}' has {1} lines of code, exceeding the {2}-line limit. Refactor and extract procedural parts into smaller, well-named private methods.";
        private static readonly LocalizableString Description = "Keep methods focused by limiting non-empty body lines when scaffold.SCA2003.max_lines is set. Fluent/builder continuation lines (leading '.') do not count. Refactor by extracting steps into well-named methods.";

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

            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;
            if (!AnalyzerConfig.TryGetPositiveInt(options, "scaffold.SCA2003.max_lines", out var maxLines))
            {
                return;
            }

            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            if (methodDeclaration.Body == null)
            {
                return;
            }

            var lineCount = CountNonEmptyBodyLines(methodDeclaration.Body);

            if (lineCount > maxLines)
            {
                var diagnostic = Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), methodDeclaration.Identifier.Text, lineCount, maxLines);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static int CountNonEmptyBodyLines(BlockSyntax body)
        {
            SourceText text = body.SyntaxTree.GetText();
            FileLinePositionSpan span = body.GetLocation().GetLineSpan();
            int firstBodyLine = span.StartLinePosition.Line + 1;
            int lastBodyLine = span.EndLinePosition.Line - 1;
            if (lastBodyLine < firstBodyLine)
            {
                return 0;
            }

            int count = 0;
            for (int line = firstBodyLine; line <= lastBodyLine; line++)
            {
                string lineText = text.Lines[line].ToString();
                if (string.IsNullOrWhiteSpace(lineText))
                {
                    continue;
                }

                if (IsFluentOrBuilderContinuationLine(lineText))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// Lines that only continue a member chain (typical fluent / builder formatting) do not count toward length.
        /// </summary>
        private static bool IsFluentOrBuilderContinuationLine(string lineText)
        {
            var trimmed = lineText.TrimStart();
            return trimmed.Length > 0 && trimmed[0] == '.';
        }
    }
}

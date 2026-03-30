using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    /// <summary>
    /// Unity UI: ban legacy types (default <c>UnityEngine.UI.Text</c>) in favor of replacements (default TMPro).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TextMeshProUsageAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA6002";
        private const string Category = "Unity";
        private const string ForbiddenTypesKey = "scaffold.SCA6002.forbidden_types";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Use TextMeshProUGUI instead of Text",
            "Error SCA6002: '{0}' is forbidden. Use '{1}' instead and add an assembly reference to 'Unity.TextMeshPro' when using TMPro.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Legacy UnityEngine.UI.Text should not be used. Prefer TextMeshProUGUI for all UI text rendering. Configure scaffold.SCA6002.forbidden_types for additional forbidden metadata names.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeIdentifierName, Microsoft.CodeAnalysis.CSharp.SyntaxKind.IdentifierName);
        }

        private static void AnalyzeIdentifierName(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not IdentifierNameSyntax identifierName)
            {
                return;
            }

            if (IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            {
                return;
            }

            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath))
            {
                return;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule))
            {
                return;
            }

            var symbol = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol;
            if (symbol is not INamedTypeSymbol namedType)
            {
                return;
            }

            var compilation = context.SemanticModel.Compilation;
            foreach (var (metadataName, replacement) in ParseForbiddenPairs(options))
            {
                var forbiddenType = compilation.GetTypeByMetadataName(metadataName);
                if (forbiddenType == null)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(namedType, forbiddenType))
                {
                    continue;
                }

                var diagnostic = Diagnostic.Create(rule, identifierName.GetLocation(), metadataName, replacement);
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }

        /// <summary>
        /// Semicolon-separated entries of <c>MetadataName=&gt;Replacement</c>. If <c>=&gt;</c> is omitted, replacement defaults to <c>TMPro.TextMeshProUGUI</c>.
        /// </summary>
        private static List<(string MetadataName, string Replacement)> ParseForbiddenPairs(AnalyzerConfigOptions options)
        {
            if (!options.TryGetValue(ForbiddenTypesKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return new List<(string, string)> { ("UnityEngine.UI.Text", "TMPro.TextMeshProUGUI") };
            }

            var list = new List<(string, string)>();
            foreach (var segment in raw.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var part = segment.Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                var arrow = part.IndexOf("=>", StringComparison.Ordinal);
                if (arrow < 0)
                {
                    list.Add((part, "TMPro.TextMeshProUGUI"));
                }
                else
                {
                    var left = part.Substring(0, arrow).Trim();
                    var right = part.Substring(arrow + 2).Trim();
                    if (left.Length > 0 && right.Length > 0)
                    {
                        list.Add((left, right));
                    }
                }
            }

            return list.Count > 0
                ? list
                : new List<(string, string)> { ("UnityEngine.UI.Text", "TMPro.TextMeshProUGUI") };
        }

        private static bool IsGeneratedFile(string filePath)
        {
            return ScriptPathFilters.IsGeneratedSourceFilePattern(ScriptPathFilters.Normalize(filePath));
        }
    }
}

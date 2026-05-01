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
    public sealed class PragmaWarningDisableAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA8002";
        private const string Category = "Architecture";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Runtime code should not suppress warnings with pragma disable",
            "SCA8002: Runtime code must not use '#pragma warning disable'. Fix the code path first. Use suppression only with explicit approval and documented justification.",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Prevents accidental warning suppression in runtime code. Consecutive disable/restore pairs for the same warning codes are not reported.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
        }

        private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule))
            {
                return;
            }

            var descriptor = rule;

            if (!ScriptPathFilters.IsRuntimeUnityScriptPath(context.Tree.FilePath))
            {
                return;
            }

            var root = context.Tree.GetRoot(context.CancellationToken);
            var pragmas = CollectPragmaDirectivesInSourceOrder(root);
            var skip = new bool[pragmas.Count];
            for (var i = 0; i < pragmas.Count - 1; i++)
            {
                if (skip[i])
                {
                    continue;
                }

                var disable = pragmas[i];
                if (!disable.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                {
                    continue;
                }

                var next = pragmas[i + 1];
                if (!next.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword))
                {
                    continue;
                }

                if (!MatchesWarningCodeSets(disable, next))
                {
                    continue;
                }

                if (!AreConsecutivePragmaLinesInSource(disable, next))
                {
                    continue;
                }

                skip[i] = true;
                skip[i + 1] = true;
            }

            for (var i = 0; i < pragmas.Count; i++)
            {
                if (skip[i])
                {
                    continue;
                }

                var pragma = pragmas[i];
                if (!pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                {
                    continue;
                }

                var diagnostic = Diagnostic.Create(descriptor, pragma.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static List<PragmaWarningDirectiveTriviaSyntax> CollectPragmaDirectivesInSourceOrder(SyntaxNode root)
        {
            var list = new List<PragmaWarningDirectiveTriviaSyntax>();
            foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
            {
                if (trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax pragma)
                {
                    list.Add(pragma);
                }
            }

            return list;
        }

        /// <summary>
        /// Paired exemption applies only when disable and restore are on consecutive source lines (not file-wide spans).
        /// </summary>
        private static bool AreConsecutivePragmaLinesInSource(
            PragmaWarningDirectiveTriviaSyntax disable,
            PragmaWarningDirectiveTriviaSyntax restore)
        {
            var lineA = disable.GetLocation().GetLineSpan().StartLinePosition.Line;
            var lineB = restore.GetLocation().GetLineSpan().StartLinePosition.Line;
            return lineB == lineA + 1;
        }

        private static bool MatchesWarningCodeSets(
            PragmaWarningDirectiveTriviaSyntax disable,
            PragmaWarningDirectiveTriviaSyntax restore)
        {
            var setA = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in disable.ErrorCodes)
            {
                setA.Add(code.ToString().Trim());
            }

            var setB = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in restore.ErrorCodes)
            {
                setB.Add(code.ToString().Trim());
            }

            return setA.SetEquals(setB);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    /// <summary>
    /// Shared syntax-tree analysis for SCA3004 / SCA3005 / SCA3006; each analyzer passes its rule subset.
    /// </summary>
    [Flags]
    internal enum NamespaceLayoutRuleKind
    {
        None = 0,
        Sca3004MultipleNamespaces = 1,
        Sca3004TypeOutsideBlock = 2,
        Sca3005 = 4,
        Sca3006 = 8,
    }

    internal static class NamespaceLayoutAnalysis
    {
        internal static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context, NamespaceLayoutRuleKind rules)
        {
            var provider = context.Options.AnalyzerConfigOptionsProvider;
            var options = provider.GetOptions(context.Tree);
            var globalOptions = provider.GlobalOptions;
            var filePath = context.Tree.FilePath;
            if (string.IsNullOrWhiteSpace(filePath)) return;
            if (IsGeneratedFile(filePath)) return;
            if (IsNamespaceValidationExempt(filePath)) return;

            DiagnosticDescriptor rule3005 = null!;
            var has5 = rules.HasFlag(NamespaceLayoutRuleKind.Sca3005) &&
                       AnalyzerConfig.TryGetEffectiveDescriptor(
                           options,
                           NamespaceLayoutDescriptors.NamespaceRootRule.Id,
                           NamespaceLayoutDescriptors.NamespaceRootRule,
                           out rule3005);
            DiagnosticDescriptor rule3006 = null!;
            var has6 = rules.HasFlag(NamespaceLayoutRuleKind.Sca3006) &&
                       AnalyzerConfig.TryGetEffectiveDescriptor(
                           options,
                           NamespaceLayoutDescriptors.NamespacePathRule.Id,
                           NamespaceLayoutDescriptors.NamespacePathRule,
                           out rule3006);
            DiagnosticDescriptor multipleNamespacesRule = null!;
            var has27Multi = rules.HasFlag(NamespaceLayoutRuleKind.Sca3004MultipleNamespaces) &&
                             AnalyzerConfig.TryGetEffectiveDescriptor(
                                 options,
                                 NamespaceLayoutDescriptors.MultipleTopLevelNamespacesRule.Id,
                                 NamespaceLayoutDescriptors.MultipleTopLevelNamespacesRule,
                                 out multipleNamespacesRule);
            DiagnosticDescriptor typeOutsideNamespaceRule = null!;
            var has27Outside = rules.HasFlag(NamespaceLayoutRuleKind.Sca3004TypeOutsideBlock) &&
                               AnalyzerConfig.TryGetEffectiveDescriptor(
                                   options,
                                   NamespaceLayoutDescriptors.TypeOutsideTopLevelNamespaceRule.Id,
                                   NamespaceLayoutDescriptors.TypeOutsideTopLevelNamespaceRule,
                                   out typeOutsideNamespaceRule);

            var hasAnyNamespaceRule = has5 || has6;
            if (!hasAnyNamespaceRule && !has27Multi && !has27Outside) return;

            var normalizedPath = ScriptPathFilters.Normalize(filePath);
            var skip3006ForSuffixGlob = NamespacePathResolution.IsPathIgnoredBySuffixGlobs(normalizedPath, options, globalOptions);

            if (!NamespacePathResolution.TryGetFolderSegmentsAfterContentRoot(filePath, options, globalOptions, out var relativeFolderSegments))
            {
                return;
            }

            var effectiveRoots = NamespacePathResolution.GetEffectiveAllowedRoots(options, globalOptions);
            NamespacePathResolution.TryGetNonEmptyFromTreeOrGlobal(options, globalOptions, NamespacePathResolution.RootKey, out var configuredRootSegment);

            var requiredSuffixSegments = NamespacePathResolution.GetRequiredSuffixSegments(relativeFolderSegments, options, globalOptions);
            var rootForExpectedPath = NamespacePathResolution.PickRootSegmentForExpectedPath(effectiveRoots, configuredRootSegment);
            var expectedFullNamespace = NamespacePathResolution.BuildFullNamespace(rootForExpectedPath, requiredSuffixSegments);
            var allowedRootsDisplay = FormatAllowedRootsList(effectiveRoots);

            var root = context.Tree.GetRoot(context.CancellationToken);
            if (IsAssemblyMetadataOnlyFile(root)) return;

            if (root is not CompilationUnitSyntax compilationUnit) return;

            var namespaceDeclarations = compilationUnit
                .DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Where(static ns => ns.Parent is CompilationUnitSyntax)
                .ToList();

            if (namespaceDeclarations.Count == 0)
            {
                if (effectiveRoots.Count > 0 && has5)
                {
                    var detail = $"File has no namespace declaration. The first segment must be one of: {allowedRootsDisplay}.";
                    context.ReportDiagnostic(Diagnostic.Create(rule3005, root.GetLocation(), detail));
                }

                return;
            }

            if (namespaceDeclarations.Count > 1 && has27Multi)
            {
                var diagnostic = Diagnostic.Create(
                    multipleNamespacesRule,
                    namespaceDeclarations[1].Name.GetLocation(),
                    namespaceDeclarations.Count);
                context.ReportDiagnostic(diagnostic);
            }

            if (has27Outside && HasBlockNamespaceAtCompilationUnitLevel(compilationUnit))
            {
                ReportTopLevelTypesOutsideNamespaceBlock(context, compilationUnit, typeOutsideNamespaceRule);
            }

            if (!has5 && !has6) return;

            if (effectiveRoots.Count == 0)
            {
                return;
            }

            foreach (var namespaceDeclaration in namespaceDeclarations)
            {
                var declaredNamespace = namespaceDeclaration.Name.ToString();
                var location = namespaceDeclaration.Name.GetLocation();

                if (has5)
                {
                    if (!NamespacePathResolution.IsFirstSegmentAllowed(declaredNamespace, effectiveRoots))
                    {
                        var first = NamespacePathResolution.GetFirstNamespaceSegment(declaredNamespace);
                        var detail = first.Length == 0
                            ? $"The namespace has an empty first segment. Use one of: {allowedRootsDisplay}."
                            : $"Namespace begins with '{first}', which is not allowed. Use one of: {allowedRootsDisplay}.";
                        context.ReportDiagnostic(Diagnostic.Create(rule3005, location, detail));
                    }
                }

                if (has6 && !skip3006ForSuffixGlob)
                {
                    if (!NamespacePathResolution.MatchesDeclaredAgainstAllowedRootsAndSuffix(
                            declaredNamespace,
                            requiredSuffixSegments,
                            effectiveRoots))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(rule3006, location, declaredNamespace, expectedFullNamespace));
                    }
                }
            }
        }

        private static string FormatAllowedRootsList(HashSet<string> effectiveRoots)
        {
            if (effectiveRoots.Count == 0)
            {
                return "(none configured)";
            }

            return string.Join(", ", effectiveRoots.OrderBy(s => s, StringComparer.Ordinal));
        }

        private static bool HasBlockNamespaceAtCompilationUnitLevel(CompilationUnitSyntax compilationUnit)
        {
            return compilationUnit.Members.OfType<NamespaceDeclarationSyntax>().Any();
        }

        private static void ReportTopLevelTypesOutsideNamespaceBlock(
            SyntaxTreeAnalysisContext context,
            CompilationUnitSyntax compilationUnit,
            DiagnosticDescriptor typeOutsideNamespaceRule)
        {
            foreach (var member in compilationUnit.Members)
            {
                if (member is BaseNamespaceDeclarationSyntax) continue;
                if (member is GlobalStatementSyntax) continue;
                if (member is DelegateDeclarationSyntax dlg)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        typeOutsideNamespaceRule,
                        dlg.Identifier.GetLocation(),
                        dlg.Identifier.Text));
                    continue;
                }

                if (member is BaseTypeDeclarationSyntax typeDecl)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        typeOutsideNamespaceRule,
                        typeDecl.Identifier.GetLocation(),
                        typeDecl.Identifier.Text));
                }
            }
        }

        private static bool IsGeneratedFile(string filePath)
        {
            var normalized = ScriptPathFilters.Normalize(filePath);
            if (normalized.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (normalized.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            var fileName = normalized.Split('/').LastOrDefault() ?? string.Empty;
            if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static bool IsNamespaceValidationExempt(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            var normalized = ScriptPathFilters.Normalize(filePath);
            var fileName = normalized.Split('/').LastOrDefault() ?? string.Empty;
            return string.Equals(fileName, "IsExternalInit.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAssemblyMetadataOnlyFile(SyntaxNode root)
        {
            if (root is not CompilationUnitSyntax compilationUnit) return false;
            if (compilationUnit.Members.Count > 0) return false;
            if (compilationUnit.AttributeLists.Count == 0) return false;

            foreach (var attributeList in compilationUnit.AttributeLists)
            {
                if (attributeList.Target == null) return false;
                var targetKind = attributeList.Target.Identifier.Kind();
                if (targetKind != SyntaxKind.AssemblyKeyword && targetKind != SyntaxKind.ModuleKeyword)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

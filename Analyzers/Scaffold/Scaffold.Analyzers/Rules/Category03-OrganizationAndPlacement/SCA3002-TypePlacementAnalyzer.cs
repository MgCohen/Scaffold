using System;
using System.Collections.Generic;
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
    public sealed class TypePlacementAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA3002";
        private const string Category = "Structure";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "One top-level type per file",
            "Error SCA3002: Type '{0}' in file '{1}' violates placement rules. {2}.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Each Unity script file may contain at most one top-level type (class, struct, interface, enum, record), except that multiple top-level types with the same simple name and distinct type parameter arities (non-generic and generic overloads) may share a file. Nested types are allowed. Move other extra types to their own files or nest them inside the primary type.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeCompilationUnit, SyntaxKind.CompilationUnit);
        }

        private void AnalyzeCompilationUnit(SyntaxNodeAnalysisContext context)
        {
            var compilationUnit = (CompilationUnitSyntax)context.Node;
            var syntaxTree = compilationUnit.SyntaxTree;
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;
            var descriptor = rule;

            if (!ScriptPathFilters.IsUnityScriptPath(syntaxTree.FilePath)) return;

            var topLevelTypes = GetTopLevelTypes(compilationUnit);
            if (topLevelTypes.Count <= 1) return;

            var fileName = ScriptPathFilters.GetFileName(syntaxTree.FilePath);
            var fileTypeName = ScriptPathFilters.GetFileNameWithoutExtension(syntaxTree.FilePath);
            var primaryType = FindPrimaryType(topLevelTypes, fileTypeName);

            foreach (var extraType in topLevelTypes)
            {
                if (extraType == primaryType) continue;
                if (IsSameNameDistinctAritySibling(extraType, topLevelTypes)) continue;

                var fixMessage =
                    $"Move '{extraType.Identifier.Text}' to its own file (e.g. '{extraType.Identifier.Text}.cs'). Only one top-level type is allowed in '{fileName}'; nested types may remain inside '{primaryType.Identifier.Text}'.";
                ReportDiagnostic(context, descriptor, extraType, fileName, fixMessage);
            }
        }

        private static List<BaseTypeDeclarationSyntax> GetTopLevelTypes(SyntaxNode root)
        {
            return root
                .DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Where(IsTopLevelType)
                .ToList();
        }

        private static bool IsTopLevelType(BaseTypeDeclarationSyntax declaration)
        {
            var parent = declaration.Parent;
            if (parent is CompilationUnitSyntax) return true;
            if (parent is NamespaceDeclarationSyntax) return true;
            if (parent is FileScopedNamespaceDeclarationSyntax) return true;
            return false;
        }

        private static BaseTypeDeclarationSyntax FindPrimaryType(IReadOnlyList<BaseTypeDeclarationSyntax> topLevelTypes, string fileTypeName)
        {
            foreach (var type in topLevelTypes)
            {
                if (string.Equals(type.Identifier.Text, fileTypeName, StringComparison.Ordinal))
                {
                    return type;
                }
            }

            return topLevelTypes[0];
        }

        /// <summary>
        /// Another top-level type in the file shares this simple name but has a different type parameter arity
        /// (e.g. <c>IRequestHandler</c>, <c>IRequestHandler&lt;T&gt;</c>, <c>IRequestHandler&lt;T1,T2&gt;</c>).
        /// </summary>
        private static bool IsSameNameDistinctAritySibling(
            BaseTypeDeclarationSyntax extra,
            IReadOnlyList<BaseTypeDeclarationSyntax> topLevelTypes)
        {
            var arity = GetTypeParameterArity(extra);
            foreach (var other in topLevelTypes)
            {
                if (ReferenceEquals(other, extra)) continue;
                if (!string.Equals(extra.Identifier.Text, other.Identifier.Text, StringComparison.Ordinal))
                    continue;
                if (GetTypeParameterArity(other) != arity)
                    return true;
            }

            return false;
        }

        private static int GetTypeParameterArity(BaseTypeDeclarationSyntax type)
        {
            return type is TypeDeclarationSyntax typeDecl
                ? typeDecl.TypeParameterList?.Parameters.Count ?? 0
                : 0;
        }

        private static void ReportDiagnostic(
            SyntaxNodeAnalysisContext context,
            DiagnosticDescriptor rule,
            BaseTypeDeclarationSyntax extraType,
            string fileName,
            string fixMessage)
        {
            var diagnostic = Diagnostic.Create(rule, extraType.Identifier.GetLocation(), extraType.Identifier.Text, fileName, fixMessage);
            context.ReportDiagnostic(diagnostic);
        }

    }
}

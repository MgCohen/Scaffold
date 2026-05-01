using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NamingConventionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticIdPrefix = "SCA1004"; // Private fields: camelCase, no leading _ or letter+'_' prefix (formerly SCA0009)
        public const string DiagnosticIdPascal = "SCA1005"; // Public or internal fields/props/methods PascalCase

        private const string Category = "Naming";

        private static readonly DiagnosticDescriptor PrivateFieldRule = new DiagnosticDescriptor(
            DiagnosticIdPrefix,
            "Private fields must be camelCase",
            "Error SCA1004: Private field '{0}' violates naming. Use camelCase: no leading '_' or letter+'_' prefix, and start with a lowercase letter.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Private fields must be camelCase: no leading underscore or letter+'_' prefix, and the name must start with a lowercase letter.");

        private static readonly DiagnosticDescriptor PascalRule = new DiagnosticDescriptor(
            DiagnosticIdPascal,
            "Public or internal members should be PascalCase",
            "Error SCA1005: Public or internal member '{0}' is lowercased. Change it to PascalCase.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Public and internal fields, properties, and methods should use PascalCase.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(PrivateFieldRule, PascalRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeField(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticIdPrefix, PrivateFieldRule, out var privateFieldRule);
            AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticIdPascal, PascalRule, out var pascalRule);

            var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
            bool isPublicOrInternal = IsPublicOrInternal(fieldDeclaration.Modifiers);

            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var name = variable.Identifier.Text;

                // Exempted names
                if (name == "gameObject" || name == "transform") continue;

                if (isPublicOrInternal)
                {
                    if (pascalRule != null && name.Length > 0 && !char.IsUpper(name[0]))
                    {
                        var diagnostic = Diagnostic.Create(pascalRule, variable.Identifier.GetLocation(), name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                else
                {
                    if (privateFieldRule != null &&
                        (HasDisallowedPrivateFieldPrefix(name) || (name.Length > 0 && char.IsUpper(name[0]))))
                    {
                        var diagnostic = Diagnostic.Create(privateFieldRule, variable.Identifier.GetLocation(), name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticIdPascal, PascalRule, out var pascalRule)) return;

            var propDeclaration = (PropertyDeclarationSyntax)context.Node;
            bool isPublicOrInternal = IsPublicOrInternal(propDeclaration.Modifiers);

            var name = propDeclaration.Identifier.Text;
            if (name == "gameObject" || name == "transform") return;

            if (isPublicOrInternal && name.Length > 0 && !char.IsUpper(name[0]))
            {
                var diagnostic = Diagnostic.Create(pascalRule, propDeclaration.Identifier.GetLocation(), name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticIdPascal, PascalRule, out var pascalRule)) return;

            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            if (methodDeclaration.Identifier.Text == string.Empty) return; // Constructors handled differently, but just in case
            if (!IsPublicOrInternal(methodDeclaration.Modifiers)) return;

            var name = methodDeclaration.Identifier.Text;

            // Basic heuristic: method names should be PascalCase
            if (name.Length > 0 && !char.IsUpper(name[0]))
            {
                // check if it's an operator or special method
                if (methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword) || name.StartsWith("operator")) return;

                var diagnostic = Diagnostic.Create(pascalRule, methodDeclaration.Identifier.GetLocation(), name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool HasDisallowedPrivateFieldPrefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("_")) return true;
            return name.Length >= 2
                && name[1] == '_'
                && char.IsLower(name[0])
                && char.IsLetter(name[0]);
        }

        private static bool IsPublicOrInternal(SyntaxTokenList modifiers)
        {
            return modifiers.Any(SyntaxKind.PublicKeyword) || modifiers.Any(SyntaxKind.InternalKeyword);
        }
    }
}


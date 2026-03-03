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
        public const string DiagnosticIdPrefix = "SCA0010"; // No _ or m_ in private fields
        public const string DiagnosticIdCamel = "SCA0011";  // Private fields camelCase
        public const string DiagnosticIdPascal = "SCA0012"; // Public fields/props/methods PascalCase
        
        private const string Category = "Naming";

        private static readonly DiagnosticDescriptor PrefixRule = new DiagnosticDescriptor(
            DiagnosticIdPrefix, "Private fields should not use prefixes", "Error SCA0010: Private field '{0}' uses '_' or 'm_'. Remove the prefix.", Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor CamelRule = new DiagnosticDescriptor(
            DiagnosticIdCamel, "Private fields should be camelCase", "Error SCA0011: Private field '{0}' is capitalized. Change it to camelCase.", Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor PascalRule = new DiagnosticDescriptor(
            DiagnosticIdPascal, "Public members should be PascalCase", "Error SCA0012: Public member '{0}' is lowercased. Change it to PascalCase.", Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(PrefixRule, CamelRule, PascalRule);

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
            var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
            bool isPublic = fieldDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword);

            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var name = variable.Identifier.Text;
                
                // Exempted names
                if (name == "gameObject" || name == "transform") continue;

                if (isPublic)
                {
                    if (name.Length > 0 && !char.IsUpper(name[0]))
                    {
                        var diagnostic = Diagnostic.Create(PascalRule, variable.Identifier.GetLocation(), name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                else
                {
                    if (name.StartsWith("_") || name.StartsWith("m_"))
                    {
                        var diagnostic = Diagnostic.Create(PrefixRule, variable.Identifier.GetLocation(), name);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (name.Length > 0 && char.IsUpper(name[0]))
                    {
                        var diagnostic = Diagnostic.Create(CamelRule, variable.Identifier.GetLocation(), name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            var propDeclaration = (PropertyDeclarationSyntax)context.Node;
            bool isPublic = propDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword);

            var name = propDeclaration.Identifier.Text;
            if (name == "gameObject" || name == "transform") return;

            if (isPublic && name.Length > 0 && !char.IsUpper(name[0]))
            {
                var diagnostic = Diagnostic.Create(PascalRule, propDeclaration.Identifier.GetLocation(), name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            if (methodDeclaration.Identifier.Text == string.Empty) return; // Constructors handled differently, but just in case
            
            var name = methodDeclaration.Identifier.Text;
            
            // Basic heuristic: method names should be PascalCase
            if (name.Length > 0 && !char.IsUpper(name[0]))
            {
                // check if it's an operator or special method
                if (methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword) || name.StartsWith("operator")) return;

                var diagnostic = Diagnostic.Create(PascalRule, methodDeclaration.Identifier.GetLocation(), name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

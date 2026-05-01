using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DeclarationCommentAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA1001";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "No comments on declarations allowed";
        private static readonly LocalizableString MessageFormat = "Error SCA1001: '{0}' has an invalid comment. Remove the comment entirely or change it to include 'todo' or 'sample'.";
        private static readonly LocalizableString Description = "Types, members, and other declarations must not have XML or inline comments in leading trivia. The only exceptions are comments containing 'todo' or 'sample'.";

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

            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.ConstructorDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.DestructorDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.OperatorDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.ConversionOperatorDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.EventDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.IndexerDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.StructDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.InterfaceDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.EnumDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.RecordDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.DelegateDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeDeclaration, SyntaxKind.EnumMemberDeclaration);
        }

        private void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;
            if (ModuleConventions.IsExcludedFromDeclarationCommentAnalysis(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            if (!context.Node.HasLeadingTrivia)
            {
                return;
            }

            var label = GetDeclarationLabel(context.Node);
            foreach (var trivia in context.Node.GetLeadingTrivia())
            {
                if (IsComment(trivia) && !IsAllowedComment(trivia))
                {
                    context.ReportDiagnostic(Diagnostic.Create(rule, trivia.GetLocation(), label));
                    break;
                }
            }
        }

        private static string GetDeclarationLabel(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax m => m.Identifier.Text,
                ConstructorDeclarationSyntax c => c.Identifier.Text,
                DestructorDeclarationSyntax d => d.Identifier.Text,
                OperatorDeclarationSyntax o => "operator " + o.OperatorToken.Text,
                ConversionOperatorDeclarationSyntax c => c.ImplicitOrExplicitKeyword.Text + " operator " + c.Type,
                PropertyDeclarationSyntax p => p.Identifier.Text,
                FieldDeclarationSyntax f => f.Declaration.Variables.Count > 0 ? f.Declaration.Variables[0].Identifier.Text : "field",
                EventDeclarationSyntax e => e.Identifier.Text,
                IndexerDeclarationSyntax => "this",
                ClassDeclarationSyntax c => c.Identifier.Text,
                StructDeclarationSyntax s => s.Identifier.Text,
                InterfaceDeclarationSyntax i => i.Identifier.Text,
                EnumDeclarationSyntax e => e.Identifier.Text,
                RecordDeclarationSyntax r => r.Identifier.Text,
                DelegateDeclarationSyntax d => d.Identifier.Text,
                EnumMemberDeclarationSyntax e => e.Identifier.Text,
                _ => "declaration",
            };
        }

        private static bool IsComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                   trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                   trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                   trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        }

        private static bool IsAllowedComment(SyntaxTrivia trivia)
        {
            var text = trivia.ToFullString().ToLowerInvariant();
            return text.Contains("todo") || text.Contains("sample");
        }
    }
}

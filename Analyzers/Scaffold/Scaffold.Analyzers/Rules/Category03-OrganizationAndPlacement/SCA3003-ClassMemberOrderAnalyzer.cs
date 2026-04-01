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
    public sealed class ClassMemberOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA3003";
        private const string Category = "Style";
        private const int StaticPropertyRank = 0;
        private const int ConstFieldRank = 1;
        private const int ConstructorRank = 2;
        private const int IndexerRank = 3;
        private const int PropertyRank = 4;
        private const int FieldRank = 5;
        private const int EventRank = 6;
        private const int InstanceMethodRank = 7;
        private const int StaticMethodRank = 8;
        private const int NestedPrivateTypeRank = 9;
        private const int OperatorRank = 10;
        private const int UnknownRank = 100;

        private static readonly LocalizableString Title = "Class members must follow project ordering";
        private static readonly LocalizableString MessageFormat =
            "Error SCA3003: Member '{0}' is out of order. Move it to follow this order: static properties, const fields, constructors, indexers, properties, fields, events, instance methods, static methods, private nested types, operators. Backing fields may appear directly below their property.";
        private static readonly LocalizableString Description =
            "Class members must follow the repository ordering contract to keep files predictable for humans and AI tooling.";

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
            context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.StructDeclaration);
        }

        private static void AnalyzeType(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath))
                return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule))
                return;
            var typeDeclaration = (TypeDeclarationSyntax)context.Node;
            var members = typeDeclaration.Members;

            for (var index = 1; index < members.Count; index++)
            {
                var previous = members[index - 1];
                var current = members[index];
                var previousRank = GetEffectiveRank(members, index - 1);
                var currentRank = GetEffectiveRank(members, index);

                if (currentRank >= previousRank)
                    continue;

                var memberName = GetMemberName(current);
                var diagnostic = Diagnostic.Create(rule, current.GetLocation(), memberName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static int GetEffectiveRank(SyntaxList<MemberDeclarationSyntax> members, int index)
        {
            var member = members[index];
            var rank = GetBaseRank(member);
            if (rank != FieldRank || !IsBackingFieldForPreviousProperty(members, index))
                return rank;

            return PropertyRank;
        }

        private static int GetBaseRank(MemberDeclarationSyntax member)
        {
            if (member is ConstructorDeclarationSyntax)
                return ConstructorRank;

            if (member is IndexerDeclarationSyntax)
                return IndexerRank;

            if (member is PropertyDeclarationSyntax property)
                return property.Modifiers.Any(SyntaxKind.StaticKeyword) ? StaticPropertyRank : PropertyRank;

            if (member is FieldDeclarationSyntax field)
                return field.Modifiers.Any(SyntaxKind.ConstKeyword) ? ConstFieldRank : FieldRank;

            if (member is EventDeclarationSyntax || member is EventFieldDeclarationSyntax)
                return EventRank;

            if (member is MethodDeclarationSyntax method)
                return GetMethodRank(method);

            if (member is OperatorDeclarationSyntax || member is ConversionOperatorDeclarationSyntax)
                return OperatorRank;

            if (member is TypeDeclarationSyntax nestedType && IsPrivate(nestedType.Modifiers))
                return NestedPrivateTypeRank;

            if (member is EnumDeclarationSyntax nestedEnum && IsPrivate(nestedEnum.Modifiers))
                return NestedPrivateTypeRank;

            return UnknownRank;
        }

        private static int GetMethodRank(MethodDeclarationSyntax method)
        {
            return method.Modifiers.Any(SyntaxKind.StaticKeyword) ? StaticMethodRank : InstanceMethodRank;
        }

        private static bool IsBackingFieldForPreviousProperty(
            SyntaxList<MemberDeclarationSyntax> members,
            int fieldIndex)
        {
            if (fieldIndex < 1)
                return false;

            if (!(members[fieldIndex] is FieldDeclarationSyntax field))
                return false;

            if (!(members[fieldIndex - 1] is PropertyDeclarationSyntax property))
                return false;

            var fieldName = GetSingleFieldName(field);
            if (fieldName == null)
                return false;

            var referencedNames = GetReferencedIdentifiers(property);
            return referencedNames.Contains(fieldName);
        }

        private static string? GetSingleFieldName(FieldDeclarationSyntax field)
        {
            if (field.Declaration?.Variables.Count != 1)
                return null;

            return field.Declaration.Variables[0].Identifier.Text;
        }

        private static HashSet<string> GetReferencedIdentifiers(PropertyDeclarationSyntax property)
        {
            var names = new HashSet<string>();
            var body = property.ExpressionBody?.Expression ?? GetGetterExpression(property);
            if (body == null)
                return names;

            foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                names.Add(identifier.Identifier.Text);

            return names;
        }

        private static ExpressionSyntax? GetGetterExpression(PropertyDeclarationSyntax property)
        {
            if (property.AccessorList == null)
                return null;

            var getter = property.AccessorList.Accessors
                .FirstOrDefault(accessor => accessor.Kind() == SyntaxKind.GetAccessorDeclaration);
            if (getter?.ExpressionBody != null)
                return getter.ExpressionBody.Expression;

            var returnStatement = getter?.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault();
            return returnStatement?.Expression;
        }

        private static bool IsPrivate(SyntaxTokenList modifiers)
        {
            return modifiers.Any(SyntaxKind.PrivateKeyword);
        }

        private static string GetMemberName(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.Text;
                case ConstructorDeclarationSyntax constructor:
                    return constructor.Identifier.Text;
                case PropertyDeclarationSyntax property:
                    return property.Identifier.Text;
                case FieldDeclarationSyntax field:
                    return field.Declaration?.Variables.FirstOrDefault()?.Identifier.Text ?? "field";
                case EventDeclarationSyntax @event:
                    return @event.Identifier.Text;
                case EventFieldDeclarationSyntax @event:
                    return @event.Declaration?.Variables.FirstOrDefault()?.Identifier.Text ?? "event";
                case IndexerDeclarationSyntax _:
                    return "this[]";
                case TypeDeclarationSyntax nestedType:
                    return nestedType.Identifier.Text;
                case EnumDeclarationSyntax nestedEnum:
                    return nestedEnum.Identifier.Text;
                case OperatorDeclarationSyntax _:
                    return "operator";
                case ConversionOperatorDeclarationSyntax _:
                    return "conversion operator";
                default:
                    return member.Kind().ToString();
            }
        }
    }
}


using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    /// <summary>
    /// Unity serialization: require <see cref="SerializeField"/> backing fields for
    /// <c>public get / private set</c> properties on <see cref="SerializableAttribute"/> or <c>UnityEngine.Object</c> types.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SerializableBackingFieldAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA6001";
        private const string Category = "Unity";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Unity types: use [SerializeField] backing for public get / private set properties",
            "Error SCA6001: Public property '{0}' uses 'public get; private set;' but must use a private [SerializeField] field and a public getter-only property (e.g. `[SerializeField] private int field;` and `public int Prop => field;`).",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "For [Serializable] or UnityEngine.Object types, replace public get/private set auto-properties with private [SerializeField] fields and public getter-only properties.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;
            var descriptor = rule;

            if (!IsUnityFacingCompilation(context.Compilation)) return;

            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            if (!ScriptPathFilters.IsUnityScriptPath(classDeclaration.SyntaxTree.FilePath)) return;

            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
            if (classSymbol == null) return;
            if (!IsEligibleUnitySerializationType(context.Compilation, classSymbol, classDeclaration)) return;

            foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
            {
                var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
                if (!IsPublicGetterPrivateSetter(propertySymbol)) continue;
                if (UsesSerializedBackingFieldPattern(classDeclaration, property)) continue;

                var diagnostic = Diagnostic.Create(descriptor, property.Identifier.GetLocation(), property.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsUnityFacingCompilation(Compilation compilation)
        {
            return compilation.ReferencedAssemblyNames.Any(reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal));
        }

        /// <summary>
        /// <c>[Serializable]</c> data types, or types inheriting <c>UnityEngine.Object</c> (e.g. <c>MonoBehaviour</c>, <c>ScriptableObject</c>).
        /// </summary>
        private static bool IsEligibleUnitySerializationType(Compilation compilation, INamedTypeSymbol classSymbol, ClassDeclarationSyntax classDeclaration)
        {
            if (HasSerializableAttribute(classDeclaration)) return true;
            return InheritsUnityEngineObject(compilation, classSymbol);
        }

        private static bool HasSerializableAttribute(ClassDeclarationSyntax classDeclaration)
        {
            foreach (var attribute in classDeclaration.AttributeLists.SelectMany(list => list.Attributes))
            {
                var name = attribute.Name.ToString();
                if (name.EndsWith("Serializable", StringComparison.Ordinal) || name.EndsWith("SerializableAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InheritsUnityEngineObject(Compilation compilation, INamedTypeSymbol typeSymbol)
        {
            var unityObject = compilation.GetTypeByMetadataName("UnityEngine.Object");
            if (unityObject == null) return false;

            for (INamedTypeSymbol? current = typeSymbol; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, unityObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPublicGetterPrivateSetter(IPropertySymbol? property)
        {
            if (property == null) return false;
            if (property.IsStatic) return false;
            if (property.DeclaredAccessibility != Accessibility.Public) return false;

            var getMethod = property.GetMethod;
            var setMethod = property.SetMethod;
            if (getMethod == null || setMethod == null) return false;
            if (getMethod.DeclaredAccessibility != Accessibility.Public) return false;
            if (setMethod.DeclaredAccessibility != Accessibility.Private) return false;
            if (setMethod.IsInitOnly)
            {
                return false;
            }

            return true;
        }

        private static bool UsesSerializedBackingFieldPattern(ClassDeclarationSyntax classDeclaration, PropertyDeclarationSyntax property)
        {
            if (!HasGetterOnlySignature(property)) return false;
            if (!TryGetBackingFieldName(property, out var backingFieldName)) return false;
            if (!TryFindField(classDeclaration, backingFieldName!, out var field)) return false;
            if (!IsPrivateField(field)) return false;
            if (!HasSerializeFieldAttribute(field)) return false;

            return true;
        }

        private static bool HasGetterOnlySignature(PropertyDeclarationSyntax property)
        {
            if (property.ExpressionBody != null) return true;
            if (property.AccessorList == null) return false;

            var accessors = property.AccessorList.Accessors;
            if (accessors.Count != 1) return false;

            return accessors[0].Kind() == SyntaxKind.GetAccessorDeclaration;
        }

        private static bool TryGetBackingFieldName(PropertyDeclarationSyntax property, out string? fieldName)
        {
            fieldName = null;

            if (property.ExpressionBody?.Expression is IdentifierNameSyntax expressionIdentifier)
            {
                fieldName = expressionIdentifier.Identifier.Text;
                return true;
            }

            var getter = property.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
            if (getter?.Body == null || getter.Body.Statements.Count != 1) return false;

            if (!(getter.Body.Statements[0] is ReturnStatementSyntax returnStatement)) return false;
            if (!(returnStatement.Expression is IdentifierNameSyntax bodyIdentifier)) return false;

            fieldName = bodyIdentifier.Identifier.Text;
            return true;
        }

        private static bool TryFindField(ClassDeclarationSyntax classDeclaration, string fieldName, out FieldDeclarationSyntax field)
        {
            field = classDeclaration.Members
                .OfType<FieldDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Declaration.Variables.Any(variable => variable.Identifier.Text == fieldName));
            return field != null;
        }

        private static bool IsPrivateField(FieldDeclarationSyntax field)
        {
            return field.Modifiers.Any(SyntaxKind.PrivateKeyword);
        }

        private static bool HasSerializeFieldAttribute(FieldDeclarationSyntax field)
        {
            foreach (var attribute in field.AttributeLists.SelectMany(list => list.Attributes))
            {
                var name = attribute.Name.ToString();
                if (name.EndsWith("SerializeField", StringComparison.Ordinal) || name.EndsWith("SerializeFieldAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

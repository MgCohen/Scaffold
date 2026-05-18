using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scaffold.AutoPacker;

namespace AutoPackerGenerator
{
    internal class AutoPackSyntaxReceiver : ISyntaxContextReceiver
    {
        public Dictionary<INamedTypeSymbol, List<(IFieldSymbol Field, ITypeSymbol TargetType)>> TypeFields { get; }
            = new Dictionary<INamedTypeSymbol, List<(IFieldSymbol Field, ITypeSymbol TargetType)>>(SymbolEqualityComparer.Default);

        public List<IMethodSymbol> ExtensionMethods { get; } = new List<IMethodSymbol>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            VisitTypeDeclaration(context);
            VisitFieldDeclaration(context);
            VisitMethodDeclaration(context);
        }

        private void VisitTypeDeclaration(GeneratorSyntaxContext context)
        {
            if (!(context.Node is TypeDeclarationSyntax typeDeclaration))
                return;

            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
            if (typeSymbol == null)
                return;
            if (!HasAutoPackInHierarchy(typeSymbol))
                return;

            if (!TypeFields.ContainsKey(typeSymbol))
                TypeFields[typeSymbol] = new List<(IFieldSymbol Field, ITypeSymbol TargetType)>();
        }

        private void VisitFieldDeclaration(GeneratorSyntaxContext context)
        {
            if (!(context.Node is FieldDeclarationSyntax fieldDeclaration))
                return;
            if (fieldDeclaration.AttributeLists.Count == 0)
                return;

            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                if (fieldSymbol == null || fieldSymbol.IsStatic)
                    continue;
                if (!HasAttribute(fieldSymbol, nameof(PackedAttribute)))
                    continue;

                var containingType = fieldSymbol.ContainingType;
                if (containingType == null)
                    continue;
                if (!HasAutoPackInHierarchy(containingType))
                    continue;

                if (!TypeFields.ContainsKey(containingType))
                    TypeFields[containingType] = new List<(IFieldSymbol Field, ITypeSymbol TargetType)>();

                ITypeSymbol targetType = null;
                foreach (var attr in fieldSymbol.GetAttributes())
                {
                    if (attr.AttributeClass?.Name == nameof(PackedAttribute) || attr.AttributeClass?.Name == "Packed")
                    {
                        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol ts)
                        {
                            targetType = ts;
                        }
                        break;
                    }
                }

                TypeFields[containingType].Add((fieldSymbol, targetType));
            }
        }

        private static bool HasAutoPackInHierarchy(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (HasAttribute(current, nameof(AutoPackAttribute)))
                    return true;
            }
            return false;
        }

        private static bool HasAttribute(ISymbol symbol, string fullAttributeName)
        {
            var shortName = fullAttributeName.EndsWith("Attribute")
                ? fullAttributeName.Substring(0, fullAttributeName.Length - 9)
                : fullAttributeName;

            foreach (var attr in symbol.GetAttributes())
            {
                var name = attr.AttributeClass?.Name;
                if (name == fullAttributeName || name == shortName)
                    return true;
            }
            return false;
        }

        private void VisitMethodDeclaration(GeneratorSyntaxContext context)
        {
            if (!(context.Node is MethodDeclarationSyntax methodDeclaration))
                return;

            if (methodDeclaration.Identifier.Text != "Resolve")
                return;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;
            if (methodSymbol == null || !methodSymbol.IsStatic || !methodSymbol.IsExtensionMethod)
                return;

            if (methodSymbol.Parameters.Length < 1)
                return;

            var firstParamType = methodSymbol.Parameters[0].Type;
            if (firstParamType.Name == "IPackingHandler")
            {
                ExtensionMethods.Add(methodSymbol);
            }
        }
    }
}

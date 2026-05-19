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

        // Closed-generic references whose original definition carries [AutoPack].
        // Sourced from any GenericNameSyntax in the compilation (new T<int>(), typeof(T<int>),
        // Channel<T<int>>, : T<int>, Register<T<int>>(), …). Drives per-instantiation registry entries.
        public HashSet<INamedTypeSymbol> ClosedInstantiations { get; }
            = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            VisitTypeDeclaration(context);
            VisitFieldDeclaration(context);
            VisitMethodDeclaration(context);
            VisitGenericName(context);
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

        private void VisitGenericName(GeneratorSyntaxContext context)
        {
            if (!(context.Node is GenericNameSyntax genericName))
                return;

            var symbolInfo = context.SemanticModel.GetSymbolInfo(genericName);
            var symbol = symbolInfo.Symbol;
            if (symbol == null && symbolInfo.CandidateSymbols.Length > 0)
                symbol = symbolInfo.CandidateSymbols[0];
            if (!(symbol is INamedTypeSymbol named))
                return;

            // We only register concrete closed forms — open-or-partially-open references like
            // TagSetEvent<U> inside `class Outer<U>` aren't usable as registry keys.
            if (named.IsUnboundGenericType || ContainsOpenTypeParameter(named))
                return;

            var original = named.OriginalDefinition;
            if (original.TypeParameters.Length == 0)
                return;
            if (!HasAttribute(original, nameof(AutoPackAttribute)))
                return;

            ClosedInstantiations.Add(named);
        }

        private static bool ContainsOpenTypeParameter(INamedTypeSymbol type)
        {
            foreach (var arg in type.TypeArguments)
            {
                if (arg is ITypeParameterSymbol) return true;
                if (arg is INamedTypeSymbol inner && ContainsOpenTypeParameter(inner)) return true;
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

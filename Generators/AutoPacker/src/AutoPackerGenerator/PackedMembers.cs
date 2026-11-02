using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoPackerGenerator
{
    // A [Packed] member is a field or a property. These helpers hide the difference.
    internal static class PackedMembers
    {
        public static ITypeSymbol TypeOf(ISymbol member) =>
            member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;

        public static bool IsFieldOrProperty(ISymbol member) =>
            member is IFieldSymbol || member is IPropertySymbol;

        public static bool IsInitOnly(ISymbol member) =>
            member is IPropertySymbol property && property.SetMethod != null && property.SetMethod.IsInitOnly;

        public static bool HasSetter(ISymbol member)
        {
            if (member is IFieldSymbol field)
                return !field.IsReadOnly && !field.IsConst;
            return ((IPropertySymbol)member).SetMethod != null;
        }

        // An object initializer in another type can set the member.
        public static bool HasVisibleSetter(ISymbol member)
        {
            if (!HasSetter(member))
                return false;
            var accessibility = member is IFieldSymbol field
                ? field.DeclaredAccessibility
                : ((IPropertySymbol)member).SetMethod.DeclaredAccessibility;
            return IsVisible(accessibility);
        }

        public static bool IsVisible(Accessibility accessibility) =>
            accessibility == Accessibility.Public
            || accessibility == Accessibility.Internal
            || accessibility == Accessibility.ProtectedOrInternal;

        public static bool IsSignedInteger(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsPartial(INamedTypeSymbol type)
        {
            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is TypeDeclarationSyntax declaration
                    && declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                    return true;
            }
            return false;
        }
    }

    // A [Packed] field whose type is a class or record with [AutoPack] in its hierarchy packs inline:
    // the field stores the Packed struct of the nearest type in the chain that gets codegen (the owner).
    // EntityId : Id stores Id.Packed. Unpack makes a new object with an object initializer.
    // If the owner has one signed integer member, -1 means null: a null field packs as -1 and -1
    // unpacks as null.
    internal sealed class NestedTypes
    {
        private readonly HashSet<INamedTypeSymbol> _emitted;
        private readonly Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> _members;

        public NestedTypes(
            HashSet<INamedTypeSymbol> emitted,
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> members)
        {
            _emitted = emitted;
            _members = members;
        }

        public bool TryGetOwner(ITypeSymbol fieldType, out INamedTypeSymbol owner)
        {
            owner = null;
            if (!(fieldType is INamedTypeSymbol named) || named.TypeKind != TypeKind.Class || named.IsUnmanagedType)
                return false;

            for (var current = named; current != null; current = current.BaseType)
            {
                if (current.IsGenericType)
                    return false;
                if (_emitted.Contains(current) && _members.TryGetValue(current, out var list) && list.Count > 0)
                {
                    owner = current;
                    return true;
                }
            }
            return false;
        }

        public IReadOnlyList<(ISymbol Field, ITypeSymbol TargetType)> MembersOf(INamedTypeSymbol owner) => _members[owner];

        // The single signed integer member that carries the "no record" value -1, or null.
        public ISymbol NoneMember(INamedTypeSymbol owner)
        {
            var members = _members[owner];
            if (members.Count != 1 || members[0].TargetType != null)
                return null;
            var type = PackedMembers.TypeOf(members[0].Field);
            return PackedMembers.IsSignedInteger(type) ? members[0].Field : null;
        }

        // Null when the nested field can pack; else the reason it cannot.
        public string Problem(INamedTypeSymbol fieldType, INamedTypeSymbol owner)
        {
            if (fieldType.IsAbstract)
                return $"'{fieldType.ToDisplayString()}' is abstract; use a concrete type";

            bool hasConstructor = false;
            foreach (var constructor in fieldType.InstanceConstructors)
            {
                if (constructor.Parameters.Length == 0 && PackedMembers.IsVisible(constructor.DeclaredAccessibility))
                {
                    hasConstructor = true;
                    break;
                }
            }
            if (!hasConstructor)
                return $"'{fieldType.ToDisplayString()}' has no public parameterless constructor";

            foreach (var member in _members[owner])
            {
                if (!PackedMembers.HasVisibleSetter(member.Field))
                    return $"member '{member.Field.Name}' has no public set or init accessor";
            }
            return null;
        }
    }
}

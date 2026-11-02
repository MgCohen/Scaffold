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
    // An owner in a referenced assembly comes from metadata: it carries [AutoPack] in its chain,
    // its generated nested Packed struct is in the assembly, and it has [Packed] members.
    internal sealed class NestedTypes
    {
        private readonly IAssemblySymbol _assembly;
        private readonly HashSet<INamedTypeSymbol> _emitted;
        private readonly Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> _members;

        public NestedTypes(
            IAssemblySymbol assembly,
            HashSet<INamedTypeSymbol> emitted,
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> members)
        {
            _assembly = assembly;
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
                if (IsOwner(current))
                {
                    owner = current;
                    return true;
                }
            }
            return false;
        }

        private bool IsOwner(INamedTypeSymbol type)
        {
            if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _assembly))
                return _emitted.Contains(type) && _members.TryGetValue(type, out var list) && list.Count > 0;
            return IsMetadataOwner(type);
        }

        // The referenced assembly ran the generator on the type: the type has a nested Packed struct.
        private bool IsMetadataOwner(INamedTypeSymbol type)
        {
            if (!HasAutoPackInChain(type) || !HasPackedStruct(type))
                return false;
            if (!_members.ContainsKey(type))
                _members[type] = MetadataMembers(type);
            return _members[type].Count > 0;
        }

        private static bool HasAutoPackInChain(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var attribute in current.GetAttributes())
                {
                    var name = attribute.AttributeClass?.Name;
                    if (name == "AutoPackAttribute" || name == "AutoPack")
                        return true;
                }
            }
            return false;
        }

        private static bool HasPackedStruct(INamedTypeSymbol type)
        {
            foreach (var nested in type.GetTypeMembers("Packed"))
            {
                if (nested.TypeKind != TypeKind.Struct)
                    continue;
                foreach (var face in nested.AllInterfaces)
                {
                    if (face.Name == "IPackedStruct")
                        return true;
                }
            }
            return false;
        }

        // Inherited members first (nearest base first), then own members: the order the generator uses.
        private static List<(ISymbol Field, ITypeSymbol TargetType)> MetadataMembers(INamedTypeSymbol type)
        {
            var list = new List<(ISymbol Field, ITypeSymbol TargetType)>();
            for (var current = type.BaseType; current != null; current = current.BaseType)
                AddPackedMembers(current, list);
            AddPackedMembers(type, list);
            return list;
        }

        private static void AddPackedMembers(INamedTypeSymbol type, List<(ISymbol Field, ITypeSymbol TargetType)> list)
        {
            foreach (var member in type.GetMembers())
            {
                if (!PackedMembers.IsFieldOrProperty(member) || member.IsStatic)
                    continue;
                foreach (var attribute in member.GetAttributes())
                {
                    var name = attribute.AttributeClass?.Name;
                    if (name != "PackedAttribute" && name != "Packed")
                        continue;
                    ITypeSymbol targetType = null;
                    if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is ITypeSymbol ts)
                        targetType = ts;
                    list.Add((member, targetType));
                    break;
                }
            }
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

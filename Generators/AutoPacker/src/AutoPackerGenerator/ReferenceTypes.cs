using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace AutoPackerGenerator
{
    // A reference type is a class or interface marked [AutoPack(AsReference = true)], directly or
    // through a base class or an implemented interface. A [Packed] field of that type stores the
    // single [Packed] key member on the wire and resolves it back through IPackingHandler on unpack.
    internal static class ReferenceTypes
    {
        internal enum KeyStatus { Valid, NotClassOrInterface, NoKey, MultipleKeys, NotSignedIntegerKey }

        internal readonly struct ReferenceKey
        {
            public ReferenceKey(ITypeSymbol fieldType, ISymbol member, ITypeSymbol keyType)
            {
                FieldType = fieldType;
                Member = member;
                KeyType = keyType;
            }

            // Field type without nullable annotation, so `Entity?` and `Entity` emit the same name.
            public ITypeSymbol FieldType { get; }
            public ISymbol Member { get; }
            public ITypeSymbol KeyType { get; }

            // Cast to the declaring type when the key lives on a base or interface, so an
            // explicitly implemented interface key still binds.
            public bool NeedsCast => !SymbolEqualityComparer.Default.Equals(Member.ContainingType, FieldType);
        }

        // Walks the type, then its base classes, until it finds [AutoPack]. The first one found
        // decides: AsReference types are referenced, not packed, so they get no codegen.
        public static bool IsPackedAutoPackType(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (TryGetAutoPack(current, out var asReference))
                    return !asReference;
            }
            return false;
        }

        public static bool TryGetMarker(ITypeSymbol fieldType, out INamedTypeSymbol marker)
        {
            marker = null;
            if (!(fieldType is INamedTypeSymbol named))
                return false;

            for (var current = named; current != null; current = current.BaseType)
            {
                if (TryGetAutoPack(current, out var asReference) && asReference)
                {
                    marker = current;
                    return true;
                }
            }
            foreach (var iface in named.AllInterfaces)
            {
                if (TryGetAutoPack(iface, out var asReference) && asReference)
                {
                    marker = iface;
                    return true;
                }
            }
            return false;
        }

        public static KeyStatus TryGetKey(ITypeSymbol fieldType, INamedTypeSymbol marker, out ReferenceKey key)
        {
            key = default;
            var plainType = fieldType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            if (plainType.TypeKind != TypeKind.Class && plainType.TypeKind != TypeKind.Interface)
                return KeyStatus.NotClassOrInterface;

            var candidates = new List<ISymbol>();
            var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var owner in KeyOwners(marker))
            {
                foreach (var member in owner.GetMembers())
                {
                    if (member.IsStatic || !HasPackedAttribute(member))
                        continue;
                    if (!(member is IFieldSymbol) && !(member is IPropertySymbol))
                        continue;
                    if (seen.Add(member))
                        candidates.Add(member);
                }
            }

            if (candidates.Count == 0)
                return KeyStatus.NoKey;
            if (candidates.Count > 1)
                return KeyStatus.MultipleKeys;

            var member0 = candidates[0];
            var keyType = member0 is IFieldSymbol f ? f.Type : ((IPropertySymbol)member0).Type;
            if (!IsSignedInteger(keyType))
                return KeyStatus.NotSignedIntegerKey;

            key = new ReferenceKey(plainType, member0, keyType);
            return KeyStatus.Valid;
        }

        // The "no record" key is -1, so the key type must hold a negative value.
        private static bool IsSignedInteger(ITypeSymbol type)
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

        private static IEnumerable<INamedTypeSymbol> KeyOwners(INamedTypeSymbol marker)
        {
            for (var current = marker; current != null; current = current.BaseType)
                yield return current;
            foreach (var iface in marker.AllInterfaces)
                yield return iface;
        }

        private static bool TryGetAutoPack(INamedTypeSymbol type, out bool asReference)
        {
            asReference = false;
            foreach (var attr in type.GetAttributes())
            {
                var name = attr.AttributeClass?.Name;
                if (name != "AutoPackAttribute" && name != "AutoPack")
                    continue;
                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "AsReference" && named.Value.Value is bool value)
                        asReference = value;
                }
                return true;
            }
            return false;
        }

        private static bool HasPackedAttribute(ISymbol symbol)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var name = attr.AttributeClass?.Name;
                if (name == "PackedAttribute" || name == "Packed")
                    return true;
            }
            return false;
        }
    }
}

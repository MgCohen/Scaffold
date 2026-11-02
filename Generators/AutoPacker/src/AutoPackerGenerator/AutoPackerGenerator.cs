using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AutoPackerGenerator
{
    [Generator]
    public class AutoPackerGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor MustBeUnmanagedDiagnostic = new DiagnosticDescriptor(
            id: "CSG002",
            title: "Serialized fields must be unmanaged",
            messageFormat: "Field '{0}' must be an unmanaged type or define an unmanaged TargetType in [Packed]. Type '{1}' is managed.",
            category: "AutoPackerGenerator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor TypeParameterFieldNoTargetTypeDiagnostic = new DiagnosticDescriptor(
            id: "CSG003",
            title: "Type-parameter [Packed] fields cannot specify a TargetType",
            messageFormat: "Field '{0}' has type parameter '{1}'; [Packed(typeof(...))] target-type overrides are not supported for type-parameter fields.",
            category: "AutoPackerGenerator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidNestedTypeDiagnostic = new DiagnosticDescriptor(
            id: "CSG004",
            title: "Nested [AutoPack] field cannot pack",
            messageFormat: "Field '{0}' cannot pack inline: {1}.",
            category: "AutoPackerGenerator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor PropertyNeedsSetterDiagnostic = new DiagnosticDescriptor(
            id: "CSG005",
            title: "[Packed] properties need a set or init accessor",
            messageFormat: "Property '{0}' has no set or init accessor; unpack cannot assign it.",
            category: "AutoPackerGenerator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AutoPackSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is AutoPackSyntaxReceiver receiver))
                return;

            var inheritedFieldsByType = ComputeInheritedFields(receiver.TypeFields);
            var emittedTypes = ComputeEmittedTypes(receiver.TypeFields, inheritedFieldsByType);
            var nested = new NestedTypes(emittedTypes, ComputeAllMembers(receiver.TypeFields, inheritedFieldsByType));
            var validTypes = CollectAndEmitPartials(context, receiver, inheritedFieldsByType, emittedTypes, nested);
            var registryEntries = BuildRegistryEntries(validTypes, receiver.ClosedInstantiations);
            EmitRegistryFile(context, registryEntries);
        }

        private static List<INamedTypeSymbol> BuildRegistryEntries(
            List<INamedTypeSymbol> validTypes,
            HashSet<INamedTypeSymbol> discoveredClosedInstantiations)
        {
            var result = new List<INamedTypeSymbol>(validTypes.Count);
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // Non-generic types — keep as-is. Open generics are skipped here; their
            // closed forms come in via the discovered set below.
            foreach (var type in validTypes)
            {
                if (type.TypeParameters.Length > 0) continue;
                if (seen.Add(type)) result.Add(type);
            }

            // Only register closed forms whose open definition actually emitted a partial —
            // otherwise the registered type has no `.Packed`.
            var emittedOpenGenerics = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var type in validTypes)
            {
                if (type.TypeParameters.Length > 0) emittedOpenGenerics.Add(type);
            }

            foreach (var closed in discoveredClosedInstantiations)
            {
                if (!emittedOpenGenerics.Contains(closed.OriginalDefinition)) continue;
                if (seen.Add(closed)) result.Add(closed);
            }

            return result;
        }

        private static Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> ComputeInheritedFields(
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> typeFields)
        {
            var result = new Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>>(SymbolEqualityComparer.Default);
            foreach (var pair in typeFields)
            {
                var list = new List<(ISymbol, ITypeSymbol)>();
                for (var b = pair.Key.BaseType; b != null; b = b.BaseType)
                {
                    foreach (var member in b.GetMembers())
                    {
                        if (!PackedMembers.IsFieldOrProperty(member) || member.IsStatic)
                            continue;
                        if (!TryGetPackedAttribute(member, out var targetType))
                            continue;
                        list.Add((member, targetType));
                    }
                }
                result[pair.Key] = list;
            }
            return result;
        }

        // Inherited members first, then own members: the same order as the Packed struct.
        private static Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> ComputeAllMembers(
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> typeFields,
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> inheritedFieldsByType)
        {
            var result = new Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>>(SymbolEqualityComparer.Default);
            foreach (var pair in typeFields)
            {
                var list = new List<(ISymbol Field, ITypeSymbol TargetType)>(inheritedFieldsByType[pair.Key]);
                list.AddRange(pair.Value);
                result[pair.Key] = list;
            }
            return result;
        }

        private static bool TryGetPackedAttribute(ISymbol field, out ITypeSymbol targetType)
        {
            targetType = null;
            foreach (var attr in field.GetAttributes())
            {
                var name = attr.AttributeClass?.Name;
                if (name != "PackedAttribute" && name != "Packed")
                    continue;
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol ts)
                    targetType = ts;
                return true;
            }
            return false;
        }

        private static HashSet<INamedTypeSymbol> ComputeEmittedTypes(
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> typeFields,
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> inheritedFieldsByType)
        {
            var emitted = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var pair in typeFields)
            {
                var totalCount = pair.Value.Count + inheritedFieldsByType[pair.Key].Count;
                // Concrete (instantiable) [AutoPack] types always get codegen — even with zero
                // aggregated [Packed] fields they receive a full (empty-payload) Packed struct so
                // they can register as zero-data wire events. Abstract marker bases stay out unless
                // a field-bearing descendant pulls them in (below) for the abstract Pack/Unpack
                // surface. The discriminator is abstract vs. concrete leaf, not field count.
                if (totalCount > 0 || !pair.Key.IsAbstract)
                    emitted.Add(pair.Key);
            }
            // Field-bearing types pull in their registered ancestors so the base can provide
            // the IPackable/IUnpackable surface (abstract for abstract markers; virtual stub otherwise).
            var withAncestors = new HashSet<INamedTypeSymbol>(emitted, SymbolEqualityComparer.Default);
            foreach (var type in emitted)
            {
                for (var b = type.BaseType; b != null; b = b.BaseType)
                {
                    if (typeFields.ContainsKey(b))
                        withAncestors.Add(b);
                }
            }
            return withAncestors;
        }

        private static bool HasEmittedAncestor(INamedTypeSymbol type, HashSet<INamedTypeSymbol> emitted)
        {
            for (var b = type.BaseType; b != null; b = b.BaseType)
            {
                if (emitted.Contains(b))
                    return true;
            }
            return false;
        }

        private static bool HasEmittedDescendant(INamedTypeSymbol type, HashSet<INamedTypeSymbol> emitted)
        {
            foreach (var candidate in emitted)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, type))
                    continue;
                for (var b = candidate.BaseType; b != null; b = b.BaseType)
                {
                    if (SymbolEqualityComparer.Default.Equals(b, type))
                        return true;
                }
            }
            return false;
        }

        private static bool HasUserDeclaredVirtualOrAbstractPackOrUnpack(INamedTypeSymbol type)
        {
            for (var b = type.BaseType; b != null; b = b.BaseType)
            {
                foreach (var member in b.GetMembers())
                {
                    if (!(member is IMethodSymbol method))
                        continue;
                    if (!method.IsAbstract && !method.IsVirtual && !method.IsOverride)
                        continue;
                    if (IsPackSignature(method) || IsUnpackSignature(method))
                        return true;
                }
            }
            return false;
        }

        // PackTyped/UnpackTyped aren't virtual by default — only when a hand-written base
        // (e.g. WireEvent<TPacked>) declares them abstract/virtual, the leaf gets `override`.
        private static bool HasUserDeclaredVirtualOrAbstractTypedPackOrUnpack(INamedTypeSymbol type)
        {
            for (var b = type.BaseType; b != null; b = b.BaseType)
            {
                foreach (var member in b.GetMembers())
                {
                    if (!(member is IMethodSymbol method))
                        continue;
                    if (!method.IsAbstract && !method.IsVirtual && !method.IsOverride)
                        continue;
                    if (IsPackTypedSignature(method) || IsUnpackTypedSignature(method))
                        return true;
                }
            }
            return false;
        }

        // Did some [AutoPack] ancestor's generator-emitted partial also emit typed methods?
        // That happens for any emitted ancestor that is NOT an abstract marker and NOT a
        // generic-forwarding intermediate — i.e. it has its own/inherited [Packed] fields, OR
        // it's a concrete zero-field leaf (which now emits an empty Packed + typed methods).
        // If yes, derived must use `new` to suppress CS0108 because base.PackTyped returns
        // Base.Packed while derived.PackTyped returns Derived.Packed.
        private static bool HasEmittedAncestorEmittingTyped(
            INamedTypeSymbol type,
            HashSet<INamedTypeSymbol> emittedTypes,
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> typeFields,
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> inheritedFieldsByType)
        {
            for (var b = type.BaseType; b != null; b = b.BaseType)
            {
                var candidate = b.OriginalDefinition;
                if (!emittedTypes.Contains(candidate)) continue;
                if (IsGenericForwardingIntermediate(candidate)) continue;
                bool hasOwn = typeFields.TryGetValue(candidate, out var own) && own.Count > 0;
                bool hasInherited = inheritedFieldsByType.TryGetValue(candidate, out var inh) && inh.Count > 0;
                // Abstract markers emit only the boxed abstract Pack/Unpack — no typed methods to hide.
                bool isAbstractMarker = candidate.IsAbstract && !hasOwn && !hasInherited;
                if (isAbstractMarker) continue;
                return true;
            }
            return false;
        }

        // A generic intermediate that forwards one of its type parameters (constrained to
        // unmanaged + IPackedStruct) into the base's TPacked slot can't emit its own nested
        // Packed/PackTyped — the typed methods would have to return a concrete nested type
        // while the base abstract returns the open TPacked, which fails CS0508. The leaf
        // closes TPacked to its own .Packed and provides the typed overrides; the walker
        // still inherits the intermediate's [Packed] fields into the leaf's Packed struct.
        private static bool IsGenericForwardingIntermediate(INamedTypeSymbol type)
        {
            if (type.TypeParameters.Length == 0) return false;
            var baseType = type.BaseType;
            if (baseType == null || baseType.TypeArguments.Length == 0) return false;

            foreach (var arg in baseType.TypeArguments)
            {
                if (!(arg is ITypeParameterSymbol tp)) continue;

                bool isOwnParam = false;
                foreach (var own in type.TypeParameters)
                {
                    if (SymbolEqualityComparer.Default.Equals(own, tp)) { isOwnParam = true; break; }
                }
                if (!isOwnParam) continue;

                if (!tp.HasUnmanagedTypeConstraint) continue;

                foreach (var constraint in tp.ConstraintTypes)
                {
                    if (constraint.Name == "IPackedStruct")
                        return true;
                }
            }
            return false;
        }

        private static bool DeclaresOwnPackOrUnpack(INamedTypeSymbol type)
        {
            foreach (var member in type.GetMembers())
            {
                if (!(member is IMethodSymbol method))
                    continue;
                if (IsPackSignature(method) || IsUnpackSignature(method))
                    return true;
            }
            return false;
        }

        private static bool IsPackSignature(IMethodSymbol method)
        {
            if (method.Name != "Pack" || method.Parameters.Length != 1)
                return false;
            if (method.ReturnType.Name != "IPackedStruct")
                return false;
            return method.Parameters[0].Type.Name == "IPackingHandler";
        }

        private static bool IsUnpackSignature(IMethodSymbol method)
        {
            if (method.Name != "Unpack" || method.Parameters.Length != 2)
                return false;
            if (!method.ReturnsVoid)
                return false;
            return method.Parameters[0].Type.Name == "IPackedStruct"
                && method.Parameters[1].Type.Name == "IPackingHandler";
        }

        private static bool IsPackTypedSignature(IMethodSymbol method)
        {
            if (method.Name != "PackTyped" || method.Parameters.Length != 1)
                return false;
            return method.Parameters[0].Type.Name == "IPackingHandler";
        }

        private static bool IsUnpackTypedSignature(IMethodSymbol method)
        {
            if (method.Name != "UnpackTyped" || method.Parameters.Length != 2)
                return false;
            if (!method.ReturnsVoid)
                return false;
            return method.Parameters[1].Type.Name == "IPackingHandler";
        }

        private static List<INamedTypeSymbol> CollectAndEmitPartials(
            GeneratorExecutionContext context,
            AutoPackSyntaxReceiver receiver,
            Dictionary<INamedTypeSymbol, List<(ISymbol Field, ITypeSymbol TargetType)>> inheritedFieldsByType,
            HashSet<INamedTypeSymbol> emittedTypes,
            NestedTypes nested)
        {
            var validTypes = new List<INamedTypeSymbol>();
            foreach (var pair in receiver.TypeFields)
            {
                var typeSymbol = pair.Key;
                if (!emittedTypes.Contains(typeSymbol))
                    continue;

                var ownFields = pair.Value;
                var inheritedFields = inheritedFieldsByType[typeSymbol];

                bool hasErrors = false;
                foreach (var tuple in ownFields)
                {
                    if (!ValidateField(context, tuple, nested))
                        hasErrors = true;
                }
                if (hasErrors) continue;

                bool ancestorEmits = HasEmittedAncestor(typeSymbol, emittedTypes)
                                     || HasUserDeclaredVirtualOrAbstractPackOrUnpack(typeSymbol);
                bool descendantEmits = HasEmittedDescendant(typeSymbol, emittedTypes);
                bool typedAncestorOverride = HasUserDeclaredVirtualOrAbstractTypedPackOrUnpack(typeSymbol);
                bool typedAncestorHides = !typedAncestorOverride
                    && HasEmittedAncestorEmittingTyped(typeSymbol, emittedTypes, receiver.TypeFields, inheritedFieldsByType);
                bool isAbstractMarker = typeSymbol.IsAbstract
                                        && ownFields.Count == 0
                                        && inheritedFields.Count == 0;
                // If the user already declared Pack/Unpack on this type (Plan-C pattern: abstract
                // base with explicit interfaces + abstract members), skip emission entirely so we
                // don't duplicate their members. The user owns the contract; derived types still
                // see the abstract Pack/Unpack via HasUserDeclaredVirtualOrAbstractPackOrUnpack
                // and pick up the override modifier.
                if (isAbstractMarker && DeclaresOwnPackOrUnpack(typeSymbol))
                    continue;

                // Generic-forwarding intermediates skip codegen entirely. Their [Packed] fields
                // are still inherited into the leaf's Packed struct via the field walker above.
                if (IsGenericForwardingIntermediate(typeSymbol))
                    continue;

                EmitPartial(context, typeSymbol, ownFields, inheritedFields, receiver.ExtensionMethods, nested, ancestorEmits, descendantEmits, typedAncestorOverride, typedAncestorHides, isAbstractMarker);
                if (!isAbstractMarker)
                    validTypes.Add(typeSymbol);
            }
            return validTypes;
        }

        private static bool ValidateField(GeneratorExecutionContext context, (ISymbol Field, ITypeSymbol TargetType) tuple, NestedTypes nested)
        {
            var fieldType = PackedMembers.TypeOf(tuple.Field);
            var location = tuple.Field.Locations.Length > 0 ? tuple.Field.Locations[0] : Location.None;

            // Per-T target-type override doesn't compose — the converter can't depend on the closed T.
            if (fieldType is ITypeParameterSymbol tp && tuple.TargetType != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(TypeParameterFieldNoTargetTypeDiagnostic, location, tuple.Field.Name, tp.Name));
                return false;
            }

            // Unpack assigns the member, so a property needs a set or init accessor.
            if (!PackedMembers.HasSetter(tuple.Field))
            {
                context.ReportDiagnostic(Diagnostic.Create(PropertyNeedsSetterDiagnostic, location, tuple.Field.Name));
                return false;
            }

            var typeToCheck = tuple.TargetType ?? fieldType;
            if (typeToCheck.IsUnmanagedType)
                return true;

            if (tuple.TargetType == null && nested.TryGetOwner(fieldType, out var owner))
            {
                var problem = nested.Problem((INamedTypeSymbol)fieldType, owner);
                if (problem == null)
                    return true;
                context.ReportDiagnostic(Diagnostic.Create(InvalidNestedTypeDiagnostic, location, tuple.Field.Name, problem));
                return false;
            }

            var diagnostic = Diagnostic.Create(MustBeUnmanagedDiagnostic, location, tuple.Field.Name, typeToCheck.ToDisplayString());
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        private static void EmitRegistryFile(GeneratorExecutionContext context, List<INamedTypeSymbol> types)
        {
            var source = Emitter.EmitRegistry(types);
            context.AddSource("AutoPackerRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private static void EmitPartial(
            GeneratorExecutionContext context,
            INamedTypeSymbol typeSymbol,
            List<(ISymbol Field, ITypeSymbol TargetType)> ownFields,
            List<(ISymbol Field, ITypeSymbol TargetType)> inheritedFields,
            List<IMethodSymbol> extensionMethods,
            NestedTypes nested,
            bool ancestorEmits,
            bool descendantEmits,
            bool typedAncestorOverride,
            bool typedAncestorHides,
            bool isAbstractMarker)
        {
            var source = Emitter.EmitSource(typeSymbol, ownFields, inheritedFields, extensionMethods, nested, ancestorEmits, descendantEmits, typedAncestorOverride, typedAncestorHides, isAbstractMarker);
            // Arity suffix keeps Foo<T> and Foo from colliding in the same compilation.
            string aritySuffix = typeSymbol.TypeParameters.Length > 0 ? $"_{typeSymbol.TypeParameters.Length}" : string.Empty;
            context.AddSource($"{typeSymbol.Name}{aritySuffix}.Packed.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }
}

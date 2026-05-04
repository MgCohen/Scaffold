#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Uniform per-package metadata record. Generator-emitted into a per-package <c>&lt;Stem&gt;Catalog</c>
    /// static class with one <see cref="CatalogEntry"/> per type the framework cares about
    /// (events, commands, entry payloads, return types, …).
    ///
    /// <para>Pure metadata + a parameterless <see cref="CreateRuntime"/> factory. Per-call
    /// configuration (e.g. <c>Timing</c> on an <c>OnTrigger</c>, runner backref bindings) is NOT
    /// baked into the factory — callers invoke <see cref="CreateRuntime"/> to get a fresh runtime
    /// node and then apply whatever post-construction setup they need by writing to the runtime
    /// object directly. Keeping the factory parameter-free keeps every catalog entry uniform and
    /// avoids the catalog growing per-concern overloads.</para>
    ///
    /// <para>Some fields are role-specific and may be unused for some <see cref="CatalogKind"/>s —
    /// <see cref="ResultType"/> is only meaningful for <see cref="CatalogKind.Command"/>,
    /// <see cref="DefaultLiteral"/> for <see cref="CatalogKind.Return"/> primitives — but the
    /// surface stays uniform so consumers index the same way regardless of which subsystem they
    /// serve.</para>
    /// </summary>
    public sealed class CatalogEntry
    {
        /// <summary>Roles this entry plays. Flags — a type can serve multiple subsystems.</summary>
        public CatalogKind             Kinds          { get; }

        /// <summary>The CLR type this entry describes.</summary>
        public Type                    Type           { get; }

        /// <summary>Port descriptors derived from the type's public instance fields. Empty for
        /// types that don't expose ports (e.g., primitive Return entries).</summary>
        public IReadOnlyList<PortMeta> Ports          { get; }

        /// <summary>Parameterless factory that produces a fresh <see cref="RuntimeNode"/> instance
        /// for this entry. Closed-generic types (e.g. <c>OnTrigger&lt;DamageDealt&gt;</c>,
        /// <c>Return&lt;int&gt;</c>) are baked at generation time. Null for entries that do not
        /// participate in runtime construction (rare).</summary>
        public Func<RuntimeNode>?      CreateRuntime  { get; }

        /// <summary>For <see cref="CatalogKind.Return"/> primitives — the C# default literal
        /// expression for the type (<c>"0"</c>, <c>"false"</c>, <c>"\"\""</c>, etc.). Null otherwise.</summary>
        public string?                 DefaultLiteral { get; }

        /// <summary>For <see cref="CatalogKind.Command"/> — the result <see cref="Type"/> closed
        /// from <c>Command&lt;TResult&gt;</c>'s base. Null otherwise.</summary>
        public Type?                   ResultType     { get; }

        public CatalogEntry(
            CatalogKind kinds,
            Type type,
            IReadOnlyList<PortMeta> ports,
            Func<RuntimeNode>? createRuntime = null,
            string? defaultLiteral = null,
            Type? resultType = null)
        {
            Kinds          = kinds;
            Type           = type ?? throw new ArgumentNullException(nameof(type));
            Ports          = ports ?? throw new ArgumentNullException(nameof(ports));
            CreateRuntime  = createRuntime;
            DefaultLiteral = defaultLiteral;
            ResultType     = resultType;
        }
    }
}

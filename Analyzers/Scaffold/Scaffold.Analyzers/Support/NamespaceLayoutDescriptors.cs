using Microsoft.CodeAnalysis;

namespace Scaffold.Analyzers
{
    /// <summary>
    /// Shared <see cref="DiagnosticDescriptor"/> instances for namespace layout rules (SCA3004–SCA3006).
    /// </summary>
    internal static class NamespaceLayoutDescriptors
    {
        private const string Category = "Design";

        internal static readonly DiagnosticDescriptor NamespaceRootRule = new DiagnosticDescriptor(
            "SCA3005",
            "Namespace root segment must be allowed",
            "Error SCA3005: {0}",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The first segment of the namespace must appear in scaffold.SCA3005.root and/or scaffold.SCA3005.allowed_roots.");

        internal static readonly DiagnosticDescriptor NamespacePathRule = new DiagnosticDescriptor(
            "SCA3006",
            "Namespace must match folder path",
            "Error SCA3006: Namespace is '{0}' but this file's path implies namespace '{1}'.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The full namespace must match the folder-derived path under scaffold.SCA3006.content_roots (see NamespacePathResolution).");

        internal static readonly DiagnosticDescriptor MultipleTopLevelNamespacesRule = new DiagnosticDescriptor(
            "SCA3004",
            "One top-level namespace per file",
            "Error SCA3004: File has {0} top-level namespace declarations. Use a single top-level namespace per file",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Each C# file under Assets/Scripts must contain exactly one top-level namespace declaration. IsExternalInit.cs is exempted.");

        internal static readonly DiagnosticDescriptor TypeOutsideTopLevelNamespaceRule = new DiagnosticDescriptor(
            "SCA3004",
            "Types must appear inside the file namespace",
            "Error SCA3004: '{0}' is declared at file scope outside the namespace block; move it inside the namespace",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "When using a block-style namespace, all types and delegates must be nested inside that namespace, not as siblings after the closing brace.");
    }
}

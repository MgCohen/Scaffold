using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class Diagnostics
    {
        const string Category = "GraphFlow";

        // EFG007 — Action payload has no execution path.
        internal static readonly DiagnosticDescriptor EFG007_NoExecutionPath = new(
            id: "EFG007",
            title: "Action payload has no execution path",
            messageFormat: "Payload '{0}' implements IGraphAction<{1}> but has no execution path. Either implement IExecutable<{1}>, declare DispatcherBase + CommandBase on [GraphPackage] and extend the CommandBase, or hand-author a runtime node.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // EFG008 — Payload satisfies bindings for two different [GraphPackage] declarations.
        internal static readonly DiagnosticDescriptor EFG008_MultiPackageBinding = new(
            id: "EFG008",
            title: "Payload bound to multiple GraphPackages",
            messageFormat: "Payload '{0}' satisfies bindings for both runners '{1}' and '{2}'. Each payload must belong to exactly one [GraphPackage].",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static Location LocationOf(ISymbol symbol)
        {
            foreach (var loc in symbol.Locations)
            {
                if (loc.IsInSource)
                {
                    return loc;
                }
            }

            return Location.None;
        }
    }
}

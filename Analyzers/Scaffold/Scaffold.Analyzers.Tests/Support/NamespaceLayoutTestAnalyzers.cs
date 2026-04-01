using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Scaffold.Analyzers;

namespace Scaffold.Analyzers.Tests;

/// <summary>
/// Typical analyzer bundles for namespace layout tests (SCA3005 + SCA3006 share one syntax pass in production via separate analyzer instances).
/// </summary>
internal static class NamespaceLayoutTestAnalyzers
{
    public static readonly ImmutableArray<DiagnosticAnalyzer> RootAndPath = ImmutableArray.Create<DiagnosticAnalyzer>(
        new NamespaceRootAnalyzer(),
        new NamespacePathAnalyzer());
}

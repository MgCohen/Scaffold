using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    internal static class NamespaceLayoutAnalyzerRegistration
    {
        internal static void Register(AnalysisContext context, NamespaceLayoutRuleKind rules)
        {
            context.RegisterCompilationStartAction(startContext =>
            {
                var globalOptions = startContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
                if (AnalyzerScopeGate.ShouldSkipEntireCompilation(startContext.Compilation.AssemblyName, globalOptions))
                {
                    return;
                }

                startContext.RegisterSyntaxTreeAction(treeContext =>
                    NamespaceLayoutAnalysis.AnalyzeSyntaxTree(treeContext, rules, startContext.Compilation.AssemblyName));
            });
        }
    }
}

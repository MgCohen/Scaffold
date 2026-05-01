using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class InitializationSameLayerUsageAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA2006";
        private const string Category = "Architecture";
        private const string InitializationInterfaceName = "Scaffold.Scope.Contracts.IAsyncLayerInitializable";
        private const string AllowUsageAttributeName = "Scaffold.Scope.Contracts.AllowSameLayerInitializationUsageAttribute";
        private const string AllowCallChainAttributeName = "Scaffold.Scope.Contracts.AllowInitializationCallChainAttribute";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Restrict same-layer dependency member usage during initialization",
            "Error SCA2006: Initialization call chain for '{0}' cannot use same-layer dependency member '{1}' from '{2}'.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Same-layer dependencies may be stored or forwarded during startup initialization, but instance members must not be consumed unless explicitly exempted.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(RegisterCompilationActions);
        }

        private static void RegisterCompilationActions(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol initializationInterface = context.Compilation.GetTypeByMetadataName(InitializationInterfaceName);
            if (initializationInterface == null)
            {
                return;
            }

            context.RegisterSymbolAction(
                symbolContext => AnalyzeMethodSymbol(symbolContext, initializationInterface),
                SymbolKind.Method);
        }

        private static void AnalyzeMethodSymbol(SymbolAnalysisContext context, INamedTypeSymbol initializationInterface)
        {
            if (context.Symbol.Locations.Any(
                    location => location.IsInSource &&
                                ModuleConventions.IsExcludedThirdPartyVendorPath(location.SourceTree?.FilePath ?? string.Empty)))
            {
                return;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            if (!(context.Symbol is IMethodSymbol methodSymbol)) { return; }
            if (!IsInitializationEntryPoint(methodSymbol, initializationInterface)) { return; }
            if (HasAttribute(methodSymbol, AllowUsageAttributeName) || HasAttribute(methodSymbol.ContainingType, AllowUsageAttributeName)) { return; }
            if (!TryGetLayer(methodSymbol, out string layer)) { return; }

            var analysis = new CallChainAnalysis(
                context.Compilation,
                context.ReportDiagnostic,
                rule,
                methodSymbol,
                layer);
            analysis.Analyze();
        }

        private static bool IsInitializationEntryPoint(IMethodSymbol methodSymbol, INamedTypeSymbol initializationInterface)
        {
            if (!string.Equals(methodSymbol.Name, "InitializeAsync", StringComparison.Ordinal))
            {
                return false;
            }

            if (methodSymbol.MethodKind != MethodKind.Ordinary)
            {
                return false;
            }

            return methodSymbol.ContainingType.AllInterfaces.Any(
                iface => SymbolEqualityComparer.Default.Equals(iface, initializationInterface));
        }

        private static bool HasAttribute(ISymbol symbol, string metadataName)
        {
            return symbol.GetAttributes().Any(attribute =>
                string.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName, StringComparison.Ordinal));
        }

        private static bool TryGetLayer(ISymbol symbol, out string layer)
        {
            foreach (Location location in symbol.Locations)
            {
                if (!location.IsInSource) { continue; }
                if (TryGetLayerFromPath(location.SourceTree?.FilePath, out layer))
                {
                    return true;
                }
            }

            layer = string.Empty;
            return false;
        }

        private static bool TryGetLayerFromPath(string path, out string layer)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                layer = string.Empty;
                return false;
            }

            string normalized = path.Replace('\\', '/');
            const string marker = "/Assets/Scripts/";
            int markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                layer = string.Empty;
                return false;
            }

            string remainder = normalized.Substring(markerIndex + marker.Length);
            string[] segments = remainder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                layer = string.Empty;
                return false;
            }

            layer = segments[0];
            return true;
        }

        private sealed class CallChainAnalysis
        {
            private readonly Compilation compilation;
            private readonly Action<Diagnostic> reportDiagnostic;
            private readonly DiagnosticDescriptor rule;
            private readonly IMethodSymbol rootMethod;
            private readonly string currentLayer;
            private readonly Dictionary<IMethodSymbol, HashSet<string>> visited = new Dictionary<IMethodSymbol, HashSet<string>>(SymbolEqualityComparer.Default);

            public CallChainAnalysis(
                Compilation compilation,
                Action<Diagnostic> reportDiagnostic,
                DiagnosticDescriptor rule,
                IMethodSymbol rootMethod,
                string currentLayer)
            {
                this.compilation = compilation;
                this.reportDiagnostic = reportDiagnostic;
                this.rule = rule;
                this.rootMethod = rootMethod;
                this.currentLayer = currentLayer;
            }

            public void Analyze()
            {
                AnalyzeMethod(rootMethod, ImmutableHashSet<ISymbol>.Empty);
            }

            private void AnalyzeMethod(IMethodSymbol methodSymbol, ImmutableHashSet<ISymbol> taintedParameters)
            {
                if (HasAttribute(methodSymbol, AllowUsageAttributeName) || HasAttribute(methodSymbol.ContainingType, AllowUsageAttributeName))
                {
                    return;
                }

                string stateKey = BuildStateKey(taintedParameters);
                if (WasVisited(methodSymbol, stateKey))
                {
                    return;
                }

                MethodDeclarationSyntax methodSyntax = GetMethodSyntax(methodSymbol);
                if (methodSyntax == null)
                {
                    return;
                }

                SemanticModel semanticModel = compilation.GetSemanticModel(methodSyntax.SyntaxTree);
                OperationBlockAnalysis operationAnalysis = new OperationBlockAnalysis(
                    currentLayer,
                    methodSymbol.ContainingType,
                    rootMethod,
                    taintedParameters);
                operationAnalysis.Analyze(semanticModel, methodSyntax, ReportUsageViolation);
                AnalyzeCalleeMethods(operationAnalysis.ForwardedCalls);
            }

            private void AnalyzeCalleeMethods(IReadOnlyList<ForwardedCall> forwardedCalls)
            {
                for (int i = 0; i < forwardedCalls.Count; i++)
                {
                    ForwardedCall forwardedCall = forwardedCalls[i];
                    if (forwardedCall.TargetMethod == null || forwardedCall.TaintedParameters.Count == 0)
                    {
                        continue;
                    }

                    if (HasAttribute(forwardedCall.TargetMethod, AllowCallChainAttributeName))
                    {
                        continue;
                    }

                    AnalyzeMethod(forwardedCall.TargetMethod, forwardedCall.TaintedParameters);
                }
            }

            private void ReportUsageViolation(Location location, IMethodSymbol entryMethod, string memberName, string dependencyType)
            {
                Diagnostic diagnostic = Diagnostic.Create(
                    rule,
                    location,
                    entryMethod.Name,
                    memberName,
                    dependencyType);
                reportDiagnostic(diagnostic);
            }

            private bool WasVisited(IMethodSymbol methodSymbol, string stateKey)
            {
                if (!visited.TryGetValue(methodSymbol, out HashSet<string> states))
                {
                    states = new HashSet<string>(StringComparer.Ordinal);
                    visited[methodSymbol] = states;
                }

                return !states.Add(stateKey);
            }

            private static string BuildStateKey(ImmutableHashSet<ISymbol> taintedParameters)
            {
                if (taintedParameters.Count == 0)
                {
                    return "<none>";
                }

                IEnumerable<string> orderedNames = taintedParameters
                    .Select(symbol => symbol.Name)
                    .OrderBy(name => name, StringComparer.Ordinal);
                return string.Join("|", orderedNames);
            }

            private static MethodDeclarationSyntax GetMethodSyntax(IMethodSymbol methodSymbol)
            {
                for (int i = 0; i < methodSymbol.DeclaringSyntaxReferences.Length; i++)
                {
                    if (methodSymbol.DeclaringSyntaxReferences[i].GetSyntax() is MethodDeclarationSyntax syntax)
                    {
                        return syntax;
                    }
                }

                return null;
            }
        }

        private sealed class OperationBlockAnalysis
        {
            private readonly string layer;
            private readonly INamedTypeSymbol containingType;
            private readonly IMethodSymbol rootMethod;
            private readonly HashSet<ISymbol> taintedSymbols;
            private readonly List<ForwardedCall> forwardedCalls = new List<ForwardedCall>();
            private readonly HashSet<SyntaxNode> reportedNodes = new HashSet<SyntaxNode>();
            private Action<Location, IMethodSymbol, string, string> reportViolation;

            public OperationBlockAnalysis(
                string layer,
                INamedTypeSymbol containingType,
                IMethodSymbol rootMethod,
                ImmutableHashSet<ISymbol> taintedParameters)
            {
                this.layer = layer;
                this.containingType = containingType;
                this.rootMethod = rootMethod;
                taintedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                foreach (ISymbol symbol in taintedParameters)
                {
                    taintedSymbols.Add(symbol);
                }
            }

            public IReadOnlyList<ForwardedCall> ForwardedCalls => forwardedCalls;

            public void Analyze(
                SemanticModel semanticModel,
                MethodDeclarationSyntax methodSyntax,
                Action<Location, IMethodSymbol, string, string> reportViolation)
            {
                this.reportViolation = reportViolation;
                if (methodSyntax.Body != null)
                {
                    AnalyzeNodes(semanticModel, methodSyntax.Body.DescendantNodes());
                }

                if (methodSyntax.ExpressionBody != null)
                {
                    AnalyzeNodes(semanticModel, methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf());
                }
            }

            private void AnalyzeNodes(SemanticModel semanticModel, IEnumerable<SyntaxNode> nodes)
            {
                foreach (SyntaxNode node in nodes)
                {
                    IOperation operation = semanticModel.GetOperation(node);
                    if (operation == null) { continue; }
                    AnalyzeOperation(operation);
                }
            }

            private void AnalyzeOperation(IOperation operation)
            {
                switch (operation)
                {
                    case IVariableDeclaratorOperation variableDeclarator:
                        HandleVariableDeclaration(variableDeclarator);
                        break;
                    case ISimpleAssignmentOperation assignment:
                        HandleAssignment(assignment);
                        break;
                    case IInvocationOperation invocation:
                        HandleInvocation(invocation);
                        break;
                    case IPropertyReferenceOperation propertyReference:
                        HandleMemberReference(propertyReference.Instance, propertyReference.Property, propertyReference);
                        break;
                    case IFieldReferenceOperation fieldReference:
                        HandleMemberReference(fieldReference.Instance, fieldReference.Field, fieldReference);
                        break;
                }
            }

            private void HandleVariableDeclaration(IVariableDeclaratorOperation variableDeclarator)
            {
                if (!(variableDeclarator.Symbol is ILocalSymbol localSymbol))
                {
                    return;
                }

                if (variableDeclarator.Initializer == null)
                {
                    return;
                }

                if (IsDependencyExpression(variableDeclarator.Initializer.Value))
                {
                    taintedSymbols.Add(localSymbol);
                }
            }

            private void HandleAssignment(ISimpleAssignmentOperation assignment)
            {
                if (!TryGetAssignedSymbol(assignment.Target, out ISymbol targetSymbol))
                {
                    return;
                }

                if (!IsDependencyExpression(assignment.Value))
                {
                    return;
                }

                taintedSymbols.Add(targetSymbol);
            }

            private void HandleInvocation(IInvocationOperation invocation)
            {
                if (invocation.Instance != null)
                {
                    ReportIfDependencyUsage(invocation.Instance, invocation.TargetMethod, invocation);
                }

                if (invocation.TargetMethod == null)
                {
                    return;
                }

                ImmutableHashSet<ISymbol>.Builder taintedParameters = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
                for (int i = 0; i < invocation.Arguments.Length; i++)
                {
                    IArgumentOperation argument = invocation.Arguments[i];
                    if (!IsDependencyExpression(argument.Value))
                    {
                        continue;
                    }

                    IParameterSymbol parameter = argument.Parameter;
                    if (parameter != null)
                    {
                        taintedParameters.Add(parameter);
                    }
                }

                if (taintedParameters.Count == 0)
                {
                    return;
                }

                if (!invocation.TargetMethod.Locations.Any(location => location.IsInSource))
                {
                    return;
                }

                forwardedCalls.Add(new ForwardedCall(invocation.TargetMethod, taintedParameters.ToImmutable()));
            }

            private void HandleMemberReference(IOperation instance, ISymbol memberSymbol, IOperation referenceOperation)
            {
                if (!IsReadAccess(referenceOperation))
                {
                    return;
                }

                ReportIfDependencyUsage(instance, memberSymbol, referenceOperation);
            }

            private void ReportIfDependencyUsage(IOperation instance, ISymbol usedMember, IOperation operation)
            {
                if (instance == null || usedMember == null)
                {
                    return;
                }

                if (!IsDependencyExpression(instance))
                {
                    return;
                }

                if (!reportedNodes.Add(operation.Syntax))
                {
                    return;
                }

                string dependencyType = instance.Type?.ToDisplayString() ?? "unknown";
                reportViolation(operation.Syntax.GetLocation(), rootMethod, usedMember.Name, dependencyType);
            }

            private bool IsDependencyExpression(IOperation operation)
            {
                IOperation unwrapped = Unwrap(operation);
                if (!TryGetReferencedSymbol(unwrapped, out ISymbol symbol))
                {
                    return false;
                }

                if (taintedSymbols.Contains(symbol))
                {
                    return true;
                }

                if (symbol is IFieldSymbol fieldSymbol)
                {
                    if (SymbolEqualityComparer.Default.Equals(fieldSymbol.ContainingType, containingType))
                    {
                        return IsSameLayerType(fieldSymbol.Type);
                    }
                }

                if (symbol is IParameterSymbol parameterSymbol)
                {
                    return taintedSymbols.Contains(parameterSymbol);
                }

                if (symbol is ILocalSymbol localSymbol)
                {
                    return taintedSymbols.Contains(localSymbol);
                }

                return false;
            }

            private bool IsSameLayerType(ITypeSymbol typeSymbol)
            {
                if (typeSymbol == null)
                {
                    return false;
                }

                foreach (Location location in typeSymbol.Locations)
                {
                    if (!location.IsInSource)
                    {
                        continue;
                    }

                    if (TryGetLayerFromPath(location.SourceTree?.FilePath, out string dependencyLayer) &&
                        string.Equals(layer, dependencyLayer, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsReadAccess(IOperation operation)
            {
                if (!(operation.Parent is ISimpleAssignmentOperation assignment))
                {
                    return true;
                }

                return !SymbolEqualityComparer.Default.Equals(
                    GetAccessedSymbol(assignment.Target),
                    GetAccessedSymbol(operation));
            }

            private static bool TryGetAssignedSymbol(IOperation targetOperation, out ISymbol symbol)
            {
                symbol = GetAccessedSymbol(targetOperation);
                return symbol != null;
            }

            private static ISymbol GetAccessedSymbol(IOperation operation)
            {
                IOperation unwrapped = Unwrap(operation);
                if (unwrapped is ILocalReferenceOperation localReference)
                {
                    return localReference.Local;
                }

                if (unwrapped is IParameterReferenceOperation parameterReference)
                {
                    return parameterReference.Parameter;
                }

                if (unwrapped is IFieldReferenceOperation fieldReference)
                {
                    return fieldReference.Field;
                }

                if (unwrapped is IPropertyReferenceOperation propertyReference)
                {
                    return propertyReference.Property;
                }

                return null;
            }

            private static bool TryGetReferencedSymbol(IOperation operation, out ISymbol symbol)
            {
                symbol = GetAccessedSymbol(operation);
                return symbol != null;
            }

            private static IOperation Unwrap(IOperation operation)
            {
                IOperation current = operation;
                while (current is IConversionOperation conversion)
                {
                    current = conversion.Operand;
                }

                return current;
            }
        }

        private sealed class ForwardedCall
        {
            public ForwardedCall(IMethodSymbol targetMethod, ImmutableHashSet<ISymbol> taintedParameters)
            {
                TargetMethod = targetMethod;
                TaintedParameters = taintedParameters;
            }

            public IMethodSymbol TargetMethod { get; }
            public ImmutableHashSet<ISymbol> TaintedParameters { get; }
        }
    }
}

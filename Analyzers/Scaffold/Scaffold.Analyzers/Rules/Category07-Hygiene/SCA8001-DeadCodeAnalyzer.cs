using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DeadCodeInRuntimeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA8001";
        private const string Category = "Quality";
        private const string ExemptMethodNamesKey = "scaffold.SCA8001.exempt_method_names";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Runtime dead code should be removed",
            "SCA8001: Non-public {0} '{1}' appears unused by non-test code. Remove it or expose it through a supported public API/interface.",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Methods and constructors in Runtime paths that are only reachable from test-only flows should be removed unless part of public API or interface contracts. Default severity is suggestion (Info). Unity engine callbacks are exempt via built-in names and scaffold.SCA8001.exempt_method_names.");

        private static readonly HashSet<string> UnityMessageLikeNames = new(StringComparer.Ordinal)
        {
            "Awake", "FixedUpdate", "LateUpdate", "OnAnimatorIK", "OnAnimatorMove", "OnApplicationFocus",
            "OnApplicationPause", "OnApplicationQuit", "OnAudioFilterRead", "OnBecameInvisible", "OnBecameVisible",
            "OnCollisionEnter", "OnCollisionEnter2D", "OnCollisionExit", "OnCollisionExit2D", "OnCollisionStay",
            "OnCollisionStay2D", "OnControllerColliderHit", "OnDisable", "OnDrawGizmos", "OnDrawGizmosSelected",
            "OnEnable", "OnGUI", "OnJointBreak", "OnJointBreak2D", "OnMouseDown", "OnMouseDrag", "OnMouseEnter",
            "OnMouseExit", "OnMouseOver", "OnMouseUp", "OnMouseUpAsButton", "OnParticleCollision",
            "OnParticleTrigger", "OnParticleSystemStopped", "OnPostRender", "OnPreCull", "OnPreRender",
            "OnRenderImage", "OnRenderObject", "OnServerInitialized", "OnTransformChildrenChanged",
            "OnTransformParentChanged", "OnTriggerEnter", "OnTriggerEnter2D", "OnTriggerExit", "OnTriggerExit2D",
            "OnTriggerStay", "OnTriggerStay2D", "OnValidate", "Reset", "Start", "Update",
            "OnBeforeSerialize", "OnAfterDeserialize", "OnBeforeDeserialize", "OnAfterSerialize",
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var candidates = new ConcurrentBag<CandidateMember>();
            var references = new ConcurrentDictionary<IMethodSymbol, byte>(SymbolEqualityComparer.Default);

            context.RegisterSemanticModelAction(ctx => AnalyzeSemanticModel(ctx, candidates, references));

            context.RegisterCompilationEndAction(endContext =>
            {
                var options = endContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
                if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule))
                {
                    return;
                }

                foreach (var candidate in candidates)
                {
                    if (references.ContainsKey(candidate.Symbol))
                    {
                        continue;
                    }

                    var kindLabel = candidate.Symbol.MethodKind == MethodKind.Constructor ? "constructor" : "method";
                    var diagnostic = Diagnostic.Create(rule, candidate.Location, kindLabel, candidate.Symbol.Name);
                    endContext.ReportDiagnostic(diagnostic);
                }
            });
        }

        private static void AnalyzeSemanticModel(
            SemanticModelAnalysisContext context,
            ConcurrentBag<CandidateMember> candidates,
            ConcurrentDictionary<IMethodSymbol, byte> references)
        {
            var tree = context.SemanticModel.SyntaxTree;
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);

            if (ScriptPathFilters.IsSca0030RuntimeCandidatePath(tree.FilePath))
            {
                var root = tree.GetRoot(cancellationToken);
                foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    var symbol = semanticModel.GetDeclaredSymbol(method, cancellationToken) as IMethodSymbol;
                    if (symbol is null || !IsCandidate(symbol, options))
                    {
                        continue;
                    }

                    candidates.Add(new CandidateMember(symbol, method.Identifier.GetLocation()));
                }

                foreach (var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
                {
                    var symbol = semanticModel.GetDeclaredSymbol(constructor, cancellationToken) as IMethodSymbol;
                    if (symbol is null || !IsCandidate(symbol, options))
                    {
                        continue;
                    }

                    candidates.Add(new CandidateMember(symbol, constructor.Identifier.GetLocation()));
                }
            }

            if (!ScriptPathFilters.IsTestSampleOrGeneratedPath(tree.FilePath))
            {
                var root = tree.GetRoot(cancellationToken);

                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
                    if (symbol == null)
                    {
                        continue;
                    }

                    references.TryAdd(symbol.OriginalDefinition, 0);
                }

                foreach (var objectCreation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    var symbol = semanticModel.GetSymbolInfo(objectCreation, cancellationToken).Symbol as IMethodSymbol;
                    if (symbol == null)
                    {
                        continue;
                    }

                    references.TryAdd(symbol.OriginalDefinition, 0);
                }

                foreach (var implicitCreation in root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
                {
                    var symbol = semanticModel.GetSymbolInfo(implicitCreation, cancellationToken).Symbol as IMethodSymbol;
                    if (symbol == null)
                    {
                        continue;
                    }

                    references.TryAdd(symbol.OriginalDefinition, 0);
                }

                foreach (var initializer in root.DescendantNodes().OfType<ConstructorInitializerSyntax>())
                {
                    var symbol = semanticModel.GetSymbolInfo(initializer, cancellationToken).Symbol as IMethodSymbol;
                    if (symbol == null)
                    {
                        continue;
                    }

                    references.TryAdd(symbol.OriginalDefinition, 0);
                }
            }
        }

        private static bool IsCandidate(IMethodSymbol symbol, AnalyzerConfigOptions? options)
        {
            if (symbol == null)
            {
                return false;
            }

            if (symbol.MethodKind != MethodKind.Ordinary && symbol.MethodKind != MethodKind.Constructor)
            {
                return false;
            }

            if (symbol.IsAbstract || symbol.IsExtern || symbol.IsOverride)
            {
                return false;
            }

            if (IsPublicFacing(symbol.DeclaredAccessibility))
            {
                return false;
            }

            if (IsInterfaceImplementation(symbol))
            {
                return false;
            }

            if (IsExemptMethodName(symbol.Name, options))
            {
                return false;
            }

            return true;
        }

        private static bool IsExemptMethodName(string name, AnalyzerConfigOptions? options)
        {
            if (UnityMessageLikeNames.Contains(name))
            {
                return true;
            }

            if (options == null)
            {
                return false;
            }

            if (!options.TryGetValue(ExemptMethodNamesKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return AnalyzerConfig.ParseSemicolonList(raw).Any(s => string.Equals(s, name, StringComparison.Ordinal));
        }

        private static bool IsInterfaceImplementation(IMethodSymbol symbol)
        {
            if (symbol.ContainingType == null)
            {
                return false;
            }

            if (symbol.ExplicitInterfaceImplementations.Length > 0)
            {
                return true;
            }

            foreach (var interfaceType in symbol.ContainingType.AllInterfaces)
            {
                foreach (var interfaceMember in interfaceType.GetMembers().OfType<IMethodSymbol>())
                {
                    var implementation = symbol.ContainingType.FindImplementationForInterfaceMember(interfaceMember) as IMethodSymbol;
                    if (implementation != null && SymbolEqualityComparer.Default.Equals(implementation.OriginalDefinition, symbol.OriginalDefinition))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsPublicFacing(Accessibility accessibility)
        {
            return accessibility == Accessibility.Public ||
                   accessibility == Accessibility.Protected ||
                   accessibility == Accessibility.ProtectedOrInternal;
        }

        private sealed class CandidateMember
        {
            public CandidateMember(IMethodSymbol symbol, Location location)
            {
                Symbol = symbol.OriginalDefinition;
                Location = location;
            }

            public IMethodSymbol Symbol { get; }
            public Location Location { get; }
        }
    }
}

#nullable enable

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Empty package-side marker for the per-run host-services bag carried on <see cref="Flow.Scope"/>.
    /// The package intentionally knows nothing about its members — Mode-2 hosts (e.g. CardSandbox)
    /// declare a member-bearing sub-interface in their own namespace and downcast to it inside their
    /// dispatcher nodes. Keeps the package un-coupled from any specific host services contract while
    /// still letting <see cref="Flow.Scope"/> be typed.
    ///
    /// <para>Mode-1 runners (no scope) leave <see cref="Flow.Scope"/> null. Mode-2 runners
    /// supply a scope-factory delegate to <c>GraphController.Initialize</c>, which the controller
    /// invokes on each Run to produce a fresh scope and assign it to <see cref="Flow.Scope"/>.</para>
    /// </summary>
    public interface IEffectScope { }
}

using System;
using Scaffold.GraphFlow.M0.Smoke;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0
{
    /// <summary>Compile-time anchor so generated <see cref="MySmokeGraphAsset"/> exists (M1 trio smoke).</summary>
    internal static class GraphPackageGeneratorSmoke
    {
        internal static readonly Type Bootstrap = typeof(MySmokeGraphAsset);
    }
}

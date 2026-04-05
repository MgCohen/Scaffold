#nullable enable

namespace Scaffold.States
{
    /// <summary>Default reference key for slices that are not keyed by a domain reference.</summary>
    public sealed class Reference : IReference
    {
        public static IReference Null { get; } = new Reference();

        private Reference()
        {
        }
    }
}

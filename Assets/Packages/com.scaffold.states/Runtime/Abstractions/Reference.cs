#nullable enable

namespace Scaffold.States
{
    public sealed class Reference : IReference
    {
        public static IReference Null { get; } = new Reference();

        private Reference()
        {
        }
    }
}

#nullable enable

namespace Scaffold.States
{
    public sealed record NullReference : Reference
    {
        public static NullReference Instance { get; } = new NullReference();

        private NullReference()
        {
        }
    }
}

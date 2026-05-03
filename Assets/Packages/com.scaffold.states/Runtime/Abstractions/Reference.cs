#nullable enable

namespace Scaffold.States
{
    public abstract record Reference
    {
        public static Reference Null => NullReference.Instance;

        public sealed record NullReference : Reference
        {
            public static NullReference Instance { get; } = new NullReference();

            private NullReference()
            {
            }
        }
    }
}

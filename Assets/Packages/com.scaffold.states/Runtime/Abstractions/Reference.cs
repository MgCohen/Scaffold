#nullable enable

namespace Scaffold.States
{
    public abstract record Reference
    {
        public static Reference Null => NullReference.Instance;
    }
}

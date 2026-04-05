#nullable enable

namespace Scaffold.States
{
    /// <summary>
    /// Callback injected into <see cref="IAggregateProvider.Wire"/> so providers can request a committed rebuild
    /// without holding a <see cref="Store"/> reference.
    /// </summary>
    public interface IAggregateRebuild
    {
        void RequestRebuild();
    }
}

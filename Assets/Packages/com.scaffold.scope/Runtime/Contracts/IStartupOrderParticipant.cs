namespace Scaffold.Scope.Contracts
{
    /// <summary>
    /// Marker only: concrete types registered as this interface participate in startup dependency ordering.
    /// Ordering is derived from inject sites and registration lookup on the built resolver; no members.
    /// </summary>
    public interface IStartupOrderParticipant
    {
    }
}

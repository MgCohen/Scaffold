namespace Sample.Turn
{
    /// <summary>
    /// Service for managing who has priority to act.
    /// </summary>
    public interface IPriorityService
    {
        void SetNextActivePlayers();
        bool IsActive(MatchPlayer player);
    }
}

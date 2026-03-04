namespace Sample.Turn
{
    /// <summary>
    /// Pure data type for a player action. Carries the player and optional payload; behaviour is defined by the PlayWindow that handles it.
    /// </summary>
    public abstract record PlayerAction(MatchPlayer Player);
}

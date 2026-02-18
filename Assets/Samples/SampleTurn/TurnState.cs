namespace Sample.Turn
{
    /// <summary>
    /// Turn state: current round, turn owner, and active phase. Use concrete types; convert to/from ids only at network boundaries.
    /// </summary>
    public record TurnState
    {
        public int CurrentRoundIndex { get; set; }
        public MatchPlayer CurrentTurnOwner { get; set; }
        public Phase CurrentPhase { get; set; }
    }
}

namespace Sample.Turn.Phases
{
    /// <summary>
    /// Example phase: on enter, prompts the current player to select a card to discard.
    /// When the player has discarded, the reactive/UI module calls <see cref="CompleteDiscard"/>,
    /// which signals completion so the turn advances.
    /// </summary>
    public class DiscardPhase : Phase
    {
        private IPhaseContext _context;

        public override void OnEnter(MatchPlayer currentPlayer, IPhaseContext context)
        {
            _context = context;
            // In a full game: set state or raise an event so the reactive/UI module shows
            // "select a card to discard" for currentPlayer. That module would then call CompleteDiscard() when done.
            // No state/events in this sample, so the phase just waits for CompleteDiscard().
        }

        public void CompleteDiscard()
        {
            _context?.Complete();
        }
    }
}

using System.Collections.Generic;

namespace Sample.Turn.Phases
{
    /// <summary>
    /// Phase that opens a PlayWindow on enter; when the window closes, the phase completes and the turn advances.
    /// </summary>
    public class PlayPhase : Phase
    {
        private readonly PlayService _playService;
        private readonly PlayWindow _playWindow;

        public PlayPhase(PlayService playService, PlayWindow playWindow)
        {
            _playService = playService;
            _playWindow = playWindow;
        }

        public override void OnEnter(IReadOnlyList<MatchPlayer> activePlayers, IPhaseContext context)
        {
            _playService.OpenWindow(_playWindow, context.Complete);
        }
    }
}

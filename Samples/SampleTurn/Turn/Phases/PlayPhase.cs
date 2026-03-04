using System.Collections.Generic;
using Sample.Turn.PlayWindows;

namespace Sample.Turn.Phases
{
    /// <summary>
    /// Phase that opens a PlayWindow on enter; when the window closes, the phase completes and the turn advances.
    /// </summary>
    public class PlayPhase : Phase
    {
        private IPlayService _playService;

        public void SetPlayService(IPlayService playService)
        {
            _playService = playService;
        }

        public override void OnEnter(IReadOnlyList<MatchPlayer> activePlayers, IPhaseContext context)
        {
            var window = new MainPlayWindow();
            _playService.OpenWindow(window, context.Complete);
        }
    }
}

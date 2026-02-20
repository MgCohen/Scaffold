using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.States;
using Sample.Turn.Mutators;

namespace Sample.Turn
{
    /// <summary>
    /// Manages play windows and action execution. Holds Store and a candidate action source; implements IPlayWindowContext for execute handlers.
    /// </summary>
    public class PlayService : IPlayWindowContext
    {
        private readonly Store _store;
        private readonly Func<MatchPlayer, IEnumerable<PlayerAction>> _getActionCandidates;
        private Action _onWindowClosed;

        public PlayService(Store store, Func<MatchPlayer, IEnumerable<PlayerAction>> getActionCandidates)
        {
            _store = store;
            _getActionCandidates = getActionCandidates;
        }

        public void OpenWindow(PlayWindow window, Action onClosed)
        {
            _onWindowClosed = onClosed;
            _store.Execute(new SetPlayWindowMutator(window));
            var initialState = window.CreateInitialState();
            _store.Execute(new SetPlayWindowStateMutator(initialState));
        }

        public void CloseWindow()
        {
            _store.Execute(new SetPlayWindowMutator(null));
            _onWindowClosed?.Invoke();
            _onWindowClosed = null;
        }

        public void ExecuteAction(PlayerAction action)
        {
            var playState = _store.Get<PlayState>();
            var window = playState.CurrentPlayWindow;
            if (window == null) return;
            window.Execute(action, this);
        }

        public TState GetWindowState<TState>() where TState : PlayWindowState
        {
            return (TState)_store.Get<PlayWindowState>();
        }

        public void SetWindowState(PlayWindowState state)
        {
            _store.Execute(new SetPlayWindowStateMutator(state));
        }

        public void PassPriority()
        {
            var turnOrder = _store.Get<TurnOrderState>();
            var priorityState = _store.Get<PriorityState>();
            var activePlayers = priorityState.ActivePlayers?.ToList() ?? new List<MatchPlayer>();
            var order = turnOrder.PlayerOrder?.ToList() ?? new List<MatchPlayer>();
            if (order.Count == 0) return;
            var current = activePlayers.Count > 0 ? activePlayers[0] : null;
            MatchPlayer next;
            if (current == null)
            {
                next = order[0];
            }
            else
            {
                var idx = order.IndexOf(current);
                if (idx < 0) return;
                next = order[(idx + 1) % order.Count];
            }
            var nextPlayers = new[] { next };
            _store.Execute(new SetActivePlayersMutator(nextPlayers));
        }

        public void KeepPriority()
        {
        }
    }
}

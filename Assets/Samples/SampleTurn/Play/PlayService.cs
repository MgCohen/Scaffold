using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.States;
using Sample.Turn.Mutators;
using UnityEngine;

namespace Sample.Turn
{
    /// <summary>
    /// Manages play windows and action execution. Holds Store; implements IPlayWindowContext for execute handlers.
    /// </summary>
    public class PlayService : IPlayService, IPlayWindowContext
    {
        private readonly Store _store;
        private Action _onWindowClosed;

        public PlayService(Store store)
        {
            _store = store;
        }

        public void OpenWindow(PlayWindow window, Action onClosed)
        {
            _onWindowClosed = onClosed;
            _store.Execute(new PushPlayWindowMutator(window));
        }

        public void CloseWindow()
        {
            _store.Execute(new PopPlayWindowMutator());
            _onWindowClosed?.Invoke();
            _onWindowClosed = null;
        }

        public async Awaitable<bool> ValidateAction(PlayerAction action)
        {
            var playState = _store.Get<PlayState>();
            var window = GetTopWindow(playState);
            if (window == null) return false;
            return await window.Validate(action);
        }

        public async Awaitable ExecuteAction(PlayerAction action)
        {
            var playState = _store.Get<PlayState>();
            var window = GetTopWindow(playState);
            if (window == null) return;
            await ExecuteActionOnWindow(window, action);
        }

        private async Awaitable ExecuteActionOnWindow(PlayWindow window, PlayerAction action)
        {
            await window.Execute(action);
        }

        private static PlayWindow GetTopWindow(PlayState playState)
        {
            if (playState.WindowStack == null || playState.WindowStack.Count == 0)
                return null;
            return playState.WindowStack[playState.WindowStack.Count - 1];
        }

        public TState GetWindowState<TState>() where TState : PlayWindowState
        {
            var playState = _store.Get<PlayState>();
            var topWindow = GetTopWindow(playState);
            if (topWindow == null || playState.WindowStates == null || !playState.WindowStates.TryGetValue(topWindow, out var windowState))
                return null;
            return (TState)windowState;
        }

        public void SetWindowState(PlayWindowState state)
        {
            var playState = _store.Get<PlayState>();
            var topWindow = GetTopWindow(playState);
            if (topWindow == null) return;
            _store.Execute(new UpdatePlayWindowStateMutator(topWindow, state));
        }
    }
}
